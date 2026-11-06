using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Mini32
{
    public sealed class Sound
    {
        public float[] Samples;
        public string FilePath;
        public double Duration;
        public bool Streaming{get{return Samples==null;}}
        public double Seconds{get{return Streaming?Duration:Samples.Length/96000.0;}}
        public static Sound Load(string file)
        {
            string ext=Path.GetExtension(file).ToLowerInvariant();if(ext!=".wav"&&ext!=".mp3")throw new InvalidDataException("Escolha WAV ou MP3.");
            using(var reader=new AudioFileReader(file)){
                if(reader.WaveFormat.Channels<1||reader.WaveFormat.Channels>2)throw new InvalidDataException("Use áudio mono ou estéreo.");
                if(reader.TotalTime.TotalSeconds>4*60*60)throw new InvalidDataException("Escolha uma faixa de até 4 horas.");
                if(reader.TotalTime.TotalSeconds>120)return new Sound{FilePath=file,Duration=reader.TotalTime.TotalSeconds};
                ISampleProvider source=reader;
                if(source.WaveFormat.SampleRate!=48000)source=new WdlResamplingSampleProvider(source,48000);
                if(source.WaveFormat.Channels==1)source=new MonoToStereoSampleProvider(source);
                var samples=new List<float>();var chunk=new float[8192];int n;
                while((n=source.Read(chunk,0,chunk.Length))>0){if(samples.Count+n>11520000)throw new InvalidDataException("Áudio acima do limite de 2 minutos.");for(int i=0;i<n;i++){float v=chunk[i];if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("Áudio inválido.");samples.Add(v);}}
                if(samples.Count<2)throw new InvalidDataException("Arquivo sem áudio.");return new Sound{Samples=samples.ToArray()};
            }
        }
    }
    // Long tracks are decoded ahead of the audio callback into a bounded two-second ring.
    // The callback only copies already decoded samples; disk and codec work stay off that thread.
    public sealed class StreamingSource : IDisposable
    {
        private readonly object sync=new object();
        private readonly float[] ring=new float[48000*2*2];
        private const int Capacity=48000*2;
        private int read,write,count;
        private volatile bool cancelled;
        private bool ended;
        private readonly Task producer;
        public string Fault;
        public StreamingSource(string file,double start,double end,bool loop)
        {
            producer=Task.Run(delegate{Produce(file,start,end,loop);});
        }
        private void Produce(string file,double start,double end,bool loop)
        {
            try{
                do{
                    long produced=0;long limit=end>0?(long)((end-start)*48000):Int64.MaxValue;
                    using(var reader=new AudioFileReader(file)){
                        reader.CurrentTime=TimeSpan.FromSeconds(start);
                        ISampleProvider source=reader;
                        if(source.WaveFormat.SampleRate!=48000)source=new WdlResamplingSampleProvider(source,48000);
                        if(source.WaveFormat.Channels==1)source=new MonoToStereoSampleProvider(source);
                        var chunk=new float[8192];int n;
                        while(!cancelled&&produced<limit){
                            int requested=limit==Int64.MaxValue?chunk.Length:(int)Math.Min(chunk.Length,(limit-produced)*2);
                            n=source.Read(chunk,0,requested);if(n<=0)break;
                            int frames=n/2,offset=0;produced+=frames;
                            while(offset<frames&&!cancelled){
                                int accepted=0;
                                lock(sync){accepted=Math.Min(frames-offset,Capacity-count);
                                    for(int i=0;i<accepted;i++){ring[write*2]=chunk[(offset+i)*2];ring[write*2+1]=chunk[(offset+i)*2+1];write=(write+1)%Capacity;}count+=accepted;}
                                offset+=accepted;if(accepted==0)Thread.Sleep(5);
                            }
                        }
                    }
                    if(produced==0)break;
                }while(loop&&!cancelled);
            }catch(Exception e){Fault=e.Message;}
            finally{lock(sync)ended=true;}
        }
        public int Read(float[] left,float[] right,int frames)
        {
            lock(sync){int n=Math.Min(frames,count);for(int i=0;i<n;i++){left[i]=ring[read*2];right[i]=ring[read*2+1];read=(read+1)%Capacity;}count-=n;return n;}
        }
        public bool Finished{get{lock(sync)return ended&&count==0;}}
        public void Dispose(){cancelled=true;}
    }
    public sealed class Voice
    {
        public readonly string Id;public readonly ActionDef Action;
        private readonly Sound sound;private readonly StreamingSource stream;private readonly NAudio.Dsp.SmbPitchShifter[] dsp={new NAudio.Dsp.SmbPitchShifter(),new NAudio.Dsp.SmbPitchShifter()};
        private readonly float[][] channels={new float[512],new float[512]};
        private int position,start,end,tail=2048,age,stopLeft=-1,stopTotal;private double current;private float duckGain=1;
        public double Pitch; public bool Done,FaultReported;
        public string Fault{get{return stream==null?null:stream.Fault;}}
        public Voice(string id,Sound sound,ActionDef action,double pitch)
        {
            Id=id;this.sound=sound;Action=action.Copy();Action.Id=action.Id;Pitch=current=pitch;
            start=(int)(action.Start*48000);end=action.End<=0?(int)(sound.Seconds*48000):Math.Min((int)(sound.Seconds*48000),(int)(action.End*48000));
            if(start>=end)throw new InvalidDataException("O início deve ser menor que o fim do áudio.");position=start;
            if(sound.Streaming)stream=new StreamingSource(sound.FilePath,action.Start,action.End,action.Loop);
        }
        public void Dispose(){if(stream!=null)stream.Dispose();}
        public void Stop(){if(stopLeft>=0)return;stopTotal=stopLeft=Action.FadeOut*48;if(stopLeft==0)Done=true;}
        public void Mix(float[] output,int offset,int frames,bool duck)
        {
            if(Done)return;int processed=0;
            while(processed<frames && !Done){int n=Math.Min(512,frames-processed);bool sourceEnded=false;
                if(stream!=null){int got=stream.Read(channels[0],channels[1],n);for(int i=got;i<n;i++){channels[0][i]=channels[1][i]=0;if(stream.Finished){tail--;if(tail<=0)sourceEnded=true;}}}
                else for(int i=0;i<n;i++){
                    if(position>=end){if(Action.Loop)position=start;else{channels[0][i]=channels[1][i]=0;tail--;if(tail<=0)sourceEnded=true;continue;}}
                    channels[0][i]=sound.Samples[position*2];channels[1][i]=sound.Samples[position*2+1];position++;
                }
                if(Action.Pitch){current+=Math.Max(-1,Math.Min(1,Pitch-current));for(int c=0;c<2;c++)dsp[c].PitchShift((float)Math.Pow(2,current/12),n,2048,4,48000,channels[c]);}
                for(int i=0;i<n;i++){
                    float gain=Action.Volume/100f;int fade=Action.FadeIn*48;
                    if(fade>0)gain*=Math.Min(1,age/(float)fade);
                    if(stopLeft>=0){if(stopLeft==0){Done=true;break;}gain*=stopLeft/(float)Math.Max(1,stopTotal);stopLeft--;}
                    float target=duck&&Action.Music?0.25f:1f;duckGain+=(target-duckGain)*0.001f;gain*=duckGain;
                    output[offset+(processed+i)*2]+=channels[0][i]*gain;output[offset+(processed+i)*2+1]+=channels[1][i]*gain;age++;
                }
                if(sourceEnded)Done=true;processed+=n;
            }
        }
    }
    public sealed class Mixer : ISampleProvider,IDisposable
    {
        private readonly object sync=new object();private readonly List<Voice> voices=new List<Voice>();private WaveOutEvent output;
        private readonly float[] meter=new float[48];private int meterNext;
        public volatile float Master=0.7f,Peak;public volatile int PitchRaw=8192;public volatile string Error,StreamError;
        private string pitchId="";
        public WaveFormat WaveFormat{get{return WaveFormat.CreateIeeeFloatWaveFormat(48000,2);}}
        public int Count{get{lock(sync)return voices.Count;}}
        public string NowPlaying{get{lock(sync)return voices.Count==0?"":voices[voices.Count-1].Action.Name;}}
        public float[] Meter{get{lock(sync){var values=new float[meter.Length];for(int i=0;i<values.Length;i++)values[i]=meter[(meterNext+i)%meter.Length];return values;}}}
        public bool Playing(string id){lock(sync)return voices.Any(v=>v.Id==id&&!v.Done);}
        public static double Semitones(int raw){return Mini32Lab.AudioEngine.ToSemitones(raw);}
        public void Open(int device)
        {
            Dispose();var next=new WaveOutEvent{DeviceNumber=device,DesiredLatency=100,NumberOfBuffers=3};
            try{next.Init(new SampleToWaveProvider16(this));next.PlaybackStopped+=delegate(object s,StoppedEventArgs e){if(e.Exception!=null){Error=e.Exception.Message;StopAll();}};next.Play();output=next;Error=null;}catch{next.Dispose();throw;}
        }
        public void Play(string id,Sound sound,ActionDef action)
        {
            var voice=new Voice(id,sound,action,action.Pitch?Semitones(PitchRaw):0);
            lock(sync){foreach(var old in voices.Where(v=>v.Id==id||(!String.IsNullOrWhiteSpace(action.Group)&&v.Action.Group==action.Group)))old.Dispose();voices.RemoveAll(v=>v.Id==id||(!String.IsNullOrWhiteSpace(action.Group)&&v.Action.Group==action.Group));if(voices.Count>=8){voice.Dispose();throw new InvalidOperationException("Limite de 8 sons simultâneos. Pare um som antes de disparar outro.");}voices.Add(voice);if(action.Pitch)pitchId=id;}
        }
        public void Stop(string id){lock(sync)foreach(var v in voices.Where(v=>v.Id==id))v.Stop();}
        public void SetVolume(string id,int volume){lock(sync)foreach(var v in voices.Where(v=>v.Id==id))v.Action.Volume=volume;}
        public void StopAll(){lock(sync){foreach(var voice in voices)voice.Dispose();voices.Clear();Array.Clear(meter,0,meter.Length);meterNext=0;}Peak=0;pitchId="";}
        public int Read(float[] buffer,int offset,int count)
        {
            Array.Clear(buffer,offset,count);float peak=0;
            lock(sync){bool duck=voices.Any(v=>v.Action.Duck&&!v.Done);foreach(var voice in voices){if(voice.Id==pitchId)voice.Pitch=Semitones(PitchRaw);voice.Mix(buffer,offset,count/2,duck);if(voice.Fault!=null&&!voice.FaultReported){voice.FaultReported=true;StreamError=voice.Fault;}}foreach(var voice in voices.Where(v=>v.Done))voice.Dispose();voices.RemoveAll(v=>v.Done);}
            for(int i=0;i<count;i++){float v=buffer[offset+i]*Master;if(float.IsNaN(v)||float.IsInfinity(v))v=0;v=Math.Max(-0.98f,Math.Min(0.98f,v));buffer[offset+i]=v;peak=Math.Max(peak,Math.Abs(v));}
            lock(sync){meter[meterNext]=(float)Math.Min(1,peak*3);meterNext=(meterNext+1)%meter.Length;}
            Peak=peak;return count;
        }
        public void Dispose(){StopAll();var old=output;output=null;if(old!=null){old.Stop();old.Dispose();}}
    }
}
