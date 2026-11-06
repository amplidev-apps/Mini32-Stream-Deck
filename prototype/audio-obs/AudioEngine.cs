using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Mini32Lab
{
    public sealed class AudioClip
    {
        public float[] Samples;
        public WaveFormat Format;
        public string Name;
        public double Seconds { get { return (double)Samples.Length / Format.Channels / Format.SampleRate; } }
        public static AudioClip Load(string path)
        {
            string ext=Path.GetExtension(path).ToLowerInvariant();
            if(ext!=".wav" && ext!=".mp3") throw new InvalidOperationException("Escolha um arquivo WAV ou MP3.");
            using(var reader=new AudioFileReader(path)) {
                if(reader.WaveFormat.Channels<1 || reader.WaveFormat.Channels>2) throw new InvalidOperationException("Este teste aceita audio mono ou estereo.");
                if(reader.TotalTime.TotalSeconds>120) throw new InvalidOperationException("Neste prototipo, escolha uma vinheta de ate 2 minutos.");
                if(reader.WaveFormat.SampleRate<16000 || reader.WaveFormat.SampleRate>96000) throw new InvalidOperationException("Use audio entre 16 e 96 kHz neste teste.");
                int limit=Math.Min(reader.WaveFormat.SampleRate*reader.WaveFormat.Channels*120,12000000);
                var list=new List<float>();float[] block=new float[8192];int n;
                while((n=reader.Read(block,0,block.Length))>0) {
                    if(list.Count+n>limit) throw new InvalidOperationException("Audio excede o limite de memoria deste prototipo.");
                    for(int i=0;i<n;i++) { if(float.IsNaN(block[i]) || float.IsInfinity(block[i])) throw new InvalidDataException("Audio contem amostras invalidas.");list.Add(block[i]); }
                }
                if(list.Count==0) throw new InvalidDataException("O arquivo nao contem audio.");
                return new AudioClip { Samples=list.ToArray(),Format=reader.WaveFormat,Name=Path.GetFileName(path) };
            }
        }
    }
    public sealed class ClipSource : ISampleProvider
    {
        private readonly AudioClip clip;
        private int position;
        public ClipSource(AudioClip clip) { this.clip=clip; }
        public WaveFormat WaveFormat { get { return clip.Format; } }
        public int Read(float[] buffer,int offset,int count)
        {
            // Flush the FFT tail using bounded silence, not a change in playback speed.
            int end=clip.Samples.Length+2048*clip.Format.Channels;
            int n=Math.Min(count,end-position);
            if(n<=0)return 0;
            int copied=Math.Min(n,Math.Max(0,clip.Samples.Length-position));
            if(copied>0)Array.Copy(clip.Samples,position,buffer,offset,copied);
            if(copied<n)Array.Clear(buffer,offset+copied,n-copied);
            position+=n;return n;
        }
    }
    public sealed class PitchVoice : ISampleProvider
    {
        private readonly ClipSource source;
        private readonly NAudio.Dsp.SmbPitchShifter[] processors;
        private readonly float[][] channelBuffers;
        private double current;
        public double Target;
        public PitchVoice(AudioClip clip,double semitones) {
            current=Target=semitones;source=new ClipSource(clip);
            processors=new NAudio.Dsp.SmbPitchShifter[clip.Format.Channels];channelBuffers=new float[clip.Format.Channels][];
            for(int c=0;c<processors.Length;c++){processors[c]=new NAudio.Dsp.SmbPitchShifter();channelBuffers[c]=new float[512];}
        }
        public WaveFormat WaveFormat { get { return source.WaveFormat; } }
        public int Read(float[] buffer,int offset,int count)
        {
            int total=0;
            while(total<count) {
                int size=Math.Min(512*WaveFormat.Channels,count-total);
                current+=Math.Max(-1,Math.Min(1,Target-current));
                int n=source.Read(buffer,offset+total,size),channels=WaveFormat.Channels,frames=n/channels;
                // Process at unity as well: bypassing at zero pitch would interrupt the FFT history.
                for(int c=0;c<channels;c++){
                    for(int f=0;f<frames;f++)channelBuffers[c][f]=buffer[offset+total+f*channels+c];
                    if(frames>0)processors[c].PitchShift((float)Math.Pow(2,current/12),frames,2048,4,WaveFormat.SampleRate,channelBuffers[c]);
                    for(int f=0;f<frames;f++)buffer[offset+total+f*channels+c]=channelBuffers[c][f];
                }
                total+=n;
                if(n<size)break;
            }
            return total;
        }
    }
    public sealed class AudioEngine : ISampleProvider, IDisposable
    {
        private readonly object gate=new object();
        private WaveOutEvent output;
        private PitchVoice voice;
        private WaveFormat format=WaveFormat.CreateIeeeFloatWaveFormat(48000,2);
        public volatile float Gain=0.25f;
        public volatile float Peak;
        public volatile int PitchValue=8192;
        public volatile string OutputError;
        private int device=-2;
        public WaveFormat WaveFormat { get { return format; } }
        public bool IsPlaying { get { lock(gate)return voice!=null; } }
        public static double ToSemitones(int raw) { raw=Math.Max(0,Math.Min(16383,raw));return raw>=8192?(raw-8192)*12.0/8191:(raw-8192)*12.0/8192; }
        public void Play(AudioClip clip,int deviceNumber)
        {
            bool reconfigure=output==null || device!=deviceNumber || format.SampleRate!=clip.Format.SampleRate || format.Channels!=clip.Format.Channels;
            if(reconfigure) {
                Dispose();format=clip.Format;device=deviceNumber;OutputError=null;
                var candidate=new WaveOutEvent {DeviceNumber=deviceNumber,DesiredLatency=100,NumberOfBuffers=3};
                try { candidate.Init(new SampleToWaveProvider16(this));output=candidate;
                    candidate.PlaybackStopped+=delegate(object sender,StoppedEventArgs e) {if(e.Exception!=null){OutputError=e.Exception.Message;Stop();}};
                    candidate.Play();
                } catch {candidate.Dispose();throw;}
            }
            var next=new PitchVoice(clip,ToSemitones(PitchValue));
            lock(gate)voice=next;
        }
        public void Stop() { lock(gate)voice=null;Peak=0; }
        public int Read(float[] buffer,int offset,int count)
        {
            int got=0;float peak=0;
            lock(gate) {
                if(voice!=null) {voice.Target=ToSemitones(PitchValue);got=voice.Read(buffer,offset,count);if(got<count)voice=null;}
                for(int i=0;i<got;i++) {float v=buffer[offset+i]*Gain;v=Math.Max(-0.98f,Math.Min(0.98f,v));buffer[offset+i]=v;peak=Math.Max(peak,Math.Abs(v));}
                if(got<count)Array.Clear(buffer,offset+got,count-got);
            }
            Peak=peak;return count;
        }
        public void Dispose() {Stop();var old=output;output=null;if(old!=null){old.Stop();old.Dispose();}}
    }
}
