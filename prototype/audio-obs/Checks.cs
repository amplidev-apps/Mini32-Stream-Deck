using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using NAudio.Wave;

namespace Mini32Lab
{
    public static class LabChecks
    {
        public static AudioClip MakeTone(int seconds)
        {
            int rate=48000;var data=new float[rate*seconds];
            for(int i=0;i<data.Length;i++){double envelope=Math.Min(1,Math.Min(i/(rate*0.02),(data.Length-i)/(rate*0.02)));data[i]=(float)(0.15*Math.Sin(2*Math.PI*440*i/rate)*envelope);}
            return new AudioClip{Samples=data,Format=WaveFormat.CreateIeeeFloatWaveFormat(rate,1),Name="Som de exemplo - 440 Hz"};
        }
        private static void Assert(bool condition,string title,StringBuilder result){if(!condition)throw new Exception("FAIL: "+title);result.AppendLine("PASS: "+title);}
        public static void Run(string root)
        {
            var report=new StringBuilder();string path=Path.Combine(root,"logs","test-result.txt");
            try {
                Assert(AudioEngine.ToSemitones(0)==-12 && AudioEngine.ToSemitones(8192)==0 && AudioEngine.ToSemitones(16383)==12,"MIDI pitch maps exactly to -12/0/+12",report);
                foreach(double semitones in new double[]{-12,0,12}) {
                    var clip=MakeTone(2);var voice=new PitchVoice(clip,semitones);var samples=new List<float>();float[] buffer=new float[1024];int n;
                    var watch=Stopwatch.StartNew();
                    while((n=voice.Read(buffer,0,buffer.Length))>0){for(int i=0;i<n;i++){if(float.IsNaN(buffer[i]) || float.IsInfinity(buffer[i]))throw new Exception("Non-finite DSP output");samples.Add(buffer[i]);}}
                    watch.Stop();
                    Assert(samples.Count==clip.Samples.Length+2048,"Duration unchanged except fixed FFT tail at "+semitones,report);
                    int start=24000,end=72000,crossings=0;
                    for(int i=start+1;i<end;i++)if(samples[i-1]<=0 && samples[i]>0)crossings++;
                    double expected=440*Math.Pow(2,semitones/12);
                    Assert(Math.Abs(crossings-expected)<expected*0.06,"Tone frequency at "+semitones+" semitones: "+crossings+" Hz",report);
                    report.AppendLine("Offline processing 2 seconds: "+watch.ElapsedMilliseconds+" ms (not end-to-end latency).");
                }
                var dynamicVoice=new PitchVoice(MakeTone(2),0);float[] block=new float[1024];int read=0,k=0;
                while((k=dynamicVoice.Read(block,0,block.Length))>0){read+=k;dynamicVoice.Target=read<48000?-12:12;for(int j=0;j<k;j++)if(float.IsNaN(block[j])||float.IsInfinity(block[j]))throw new Exception("Dynamic pitch invalid output");}
                Assert(read==98048,"Dynamic pitch preserves frame count and finite output",report);
                var stereo=MakeTone(1);var stereoData=new float[stereo.Samples.Length*2];for(int i=0;i<stereo.Samples.Length;i++){stereoData[i*2]=stereo.Samples[i];stereoData[i*2+1]=0;}
                var stereoVoice=new PitchVoice(new AudioClip{Samples=stereoData,Format=WaveFormat.CreateIeeeFloatWaveFormat(48000,2),Name="stereo"},7);
                double rightEnergy=0;while((k=stereoVoice.Read(block,0,block.Length))>0)for(int i=1;i<k;i+=2)rightEnergy+=Math.Abs(block[i]);
                Assert(rightEnergy<0.001,"Stereo channels remain separated",report);
                string wav=Path.Combine(root,"logs","generated-test.wav");
                using(var writer=new WaveFileWriter(wav,WaveFormat.CreateIeeeFloatWaveFormat(48000,1))){var tone=MakeTone(1);writer.WriteSamples(tone.Samples,0,tone.Samples.Length);}
                var loaded=AudioClip.Load(wav);Assert(Math.Abs(loaded.Seconds-1)<0.001,"Real WAV decoder",report);
                string mp3=Path.Combine(root,"logs","generated-test.mp3");
                NAudio.MediaFoundation.MediaFoundationApi.Startup();
                using(var input=new WaveFileReader(wav))using(var pcm=new Wave32To16Stream(input))MediaFoundationEncoder.EncodeToMp3(pcm,mp3,128000);
                var decodedMp3=AudioClip.Load(mp3);Assert(decodedMp3.Seconds>0.95 && decodedMp3.Seconds<1.2,"Real MP3 decoder with locally generated test file",report);
                Assert(ObsClient.Auth("test-password","salt","challenge")=="3Z430HfPPGk0ZjvYPAB1t4EM70Y2vwxE4vJz3SBbx2I=","OBS authentication vector independently calculated with Node SHA256",report);
                using(var server=new MockObs())using(var client=new ObsClient()) {
                    client.Connect(server.Port,"test-password").GetAwaiter().GetResult();
                    var reply=client.Request("GetSceneList",new{}).GetAwaiter().GetResult();
                    Assert(Convert.ToString(reply["currentProgramSceneName"])=="Cena teste","WebSocket authentication, fragmented response and scene list",report);
                    client.Request("SetCurrentProgramScene",new{sceneName="Cena teste"}).GetAwaiter().GetResult();
                    bool rejected=false;try{client.Request("SetCurrentProgramScene",new{sceneName="Inexistente"}).GetAwaiter().GetResult();}catch(ObsRequestException){rejected=true;}
                    Assert(rejected && client.Connected,"Rejected OBS request does not falsely report success",report);
                    server.Finish();
                }
                report.AppendLine("No audible output or real OBS changes performed by these checks.");
                File.WriteAllText(path,report.ToString());
            }catch(Exception ex){report.AppendLine(ex.ToString());File.WriteAllText(path,report.ToString());throw;}
        }
        public static void DeviceTest(string root)
        {
            var report=new StringBuilder();report.AppendLine("Outputs: "+WaveOut.DeviceCount);
            for(int i=0;i<WaveOut.DeviceCount;i++)report.AppendLine(i+": "+WaveOut.GetCapabilities(i).ProductName);
            using(var output=new WaveOutEvent{DesiredLatency=100,NumberOfBuffers=3})using(var engine=new AudioEngine()) {
                output.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider16(engine));output.Play();Thread.Sleep(300);output.Stop();
            }
            report.AppendLine("PASS: default output initialized, rendered silence and closed.");
            File.WriteAllText(Path.Combine(root,"logs","device-test.txt"),report.ToString());
        }
    }
    // Local protocol fixture. It never connects to or changes the real OBS.
    internal sealed class MockObs : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Task server;
        public int Port {get;private set;}
        public MockObs(){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;server=Task.Run((Action)Serve);}
        private static string ReadFrame(NetworkStream stream)
        {
            int first=stream.ReadByte();if(first<0)throw new EndOfStreamException();int size=stream.ReadByte();bool masked=(size&128)!=0;int n=size&127;
            if(n==126)n=stream.ReadByte()*256+stream.ReadByte();if(n==127)throw new IOException("Fixture frame too big");
            byte[] mask=new byte[4];if(masked)ReadBytes(stream,mask);byte[] payload=new byte[n];ReadBytes(stream,payload);if(masked)for(int i=0;i<n;i++)payload[i]^=mask[i%4];return Encoding.UTF8.GetString(payload);
        }
        private static void ReadBytes(Stream stream,byte[] bytes){int offset=0;while(offset<bytes.Length){int n=stream.Read(bytes,offset,bytes.Length-offset);if(n==0)throw new EndOfStreamException();offset+=n;}}
        private static void Frame(NetworkStream stream,byte[] bytes,int opcode,bool final)
        {
            stream.WriteByte((byte)((final?128:0)|opcode));if(bytes.Length<126)stream.WriteByte((byte)bytes.Length);else{stream.WriteByte(126);stream.WriteByte((byte)(bytes.Length>>8));stream.WriteByte((byte)bytes.Length);}stream.Write(bytes,0,bytes.Length);stream.Flush();
        }
        private static void Send(NetworkStream stream,object data,bool fragment)
        {
            string text=new JavaScriptSerializer().Serialize(data);if(!fragment){Frame(stream,Encoding.UTF8.GetBytes(text),1,true);return;}int half=text.Length/2;Frame(stream,Encoding.UTF8.GetBytes(text.Substring(0,half)),1,false);Frame(stream,Encoding.UTF8.GetBytes(text.Substring(half)),0,true);
        }
        private void Serve()
        {
            using(var tcp=listener.AcceptTcpClient())using(var stream=tcp.GetStream()) {
                stream.ReadTimeout=8000;stream.WriteTimeout=8000;var header=new StringBuilder();
                while(!header.ToString().EndsWith("\r\n\r\n")){int b=stream.ReadByte();if(b<0)throw new EndOfStreamException();header.Append((char)b);if(header.Length>16000)throw new IOException("Fixture header too large");}
                string key=null;foreach(string line in header.ToString().Split(new[]{"\r\n"},StringSplitOptions.None))if(line.StartsWith("Sec-WebSocket-Key:",StringComparison.OrdinalIgnoreCase))key=line.Substring(line.IndexOf(':')+1).Trim();
                string accept;using(var sha=SHA1.Create())accept=Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                byte[] response=Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n");stream.Write(response,0,response.Length);
                Send(stream,new{op=0,d=new{rpcVersion=1,authentication=new{salt="salt",challenge="challenge"}}},false);
                var json=new JavaScriptSerializer();var identified=json.Deserialize<Dictionary<string,object>>(ReadFrame(stream));var id=(Dictionary<string,object>)identified["d"];
                if(Convert.ToString(id["authentication"])!=ObsClient.Auth("test-password","salt","challenge"))throw new Exception("Fixture authentication mismatch");
                Send(stream,new{op=2,d=new{negotiatedRpcVersion=1}},false);
                for(int i=0;i<3;i++) {
                    var request=json.Deserialize<Dictionary<string,object>>(ReadFrame(stream));var d=(Dictionary<string,object>)request["d"];string type=Convert.ToString(d["requestType"]);bool ok=true;object data=new{};
                    if(type=="GetSceneList")data=new{currentProgramSceneName="Cena teste",scenes=new[]{new{sceneName="Cena teste",sceneIndex=0}}};
                    else ok=Convert.ToString(((Dictionary<string,object>)d["requestData"])["sceneName"])=="Cena teste";
                    Send(stream,new{op=7,d=new{requestId=d["requestId"],requestStatus=new{result=ok,code=ok?100:600,comment=ok?"":"Scene missing"},responseData=data}},true);
                }
            }
        }
        public void Finish(){if(!server.Wait(9000))throw new TimeoutException("Mock OBS did not finish.");}
        public void Dispose(){listener.Stop();}
    }
}
