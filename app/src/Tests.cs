using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using System.Web.Script.Serialization;

namespace Mini32
{
    public static class Tests
    {
        private static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
        private static Sound Tone(){var s=new Sound{Samples=new float[96000]};for(int i=0;i<48000;i++)s.Samples[i*2]=s.Samples[i*2+1]=(float)(0.2*Math.Sin(2*Math.PI*440*i/48000));return s;}
        public static int Run()
        {
            try{
                Check(Mixer.Semitones(0)==-12&&Mixer.Semitones(8192)==0&&Mixer.Semitones(16383)==12,"pitch -12 / 0 / +12");
                var sound=Tone();
                foreach(int raw in new[]{0,8192,16383})using(var mixer=new Mixer()){
                    mixer.Master=1;mixer.PitchRaw=raw;mixer.Play("one",sound,new ActionDef{Volume=100,FadeOut=0});var samples=new List<float>();var buffer=new float[1024];int reads=0;
                    while(mixer.Count>0&&reads++<150){mixer.Read(buffer,0,buffer.Length);samples.AddRange(buffer);}
                    Check(mixer.Count==0&&reads>=93&&reads<=100,"duration preserved for pitch "+raw);
                    Check(samples.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Math.Abs(x)<=0.98f),"finite bounded samples "+raw);
                    int crossings=0;for(int i=24002;i<72000;i+=2)if(samples[i-2]<=0&&samples[i]>0)crossings++;
                    double expected=440*Math.Pow(2,Mixer.Semitones(raw)/12);Check(Math.Abs(crossings*2-expected)<15,"measured frequency "+(crossings*2)+" Hz");
                }
                using(var mixer=new Mixer()){
                    var a=new ActionDef{Pitch=false,Loop=true,Volume=100,FadeOut=0};mixer.Play("a",sound,a);mixer.Play("b",sound,a);Check(mixer.Count==2,"simultaneous independent sounds");mixer.Play("a",sound,a);Check(mixer.Count==2,"retrigger replaces same action");
                    var b=new float[1024];for(int i=0;i<150;i++)mixer.Read(b,0,b.Length);Check(mixer.Count==2,"loop continues beyond duration");mixer.Stop("a");mixer.Read(b,0,b.Length);Check(mixer.Count==1,"release stops only selected voice");mixer.StopAll();Check(mixer.Count==0,"stop all");
                    a.Group="exclusive";mixer.Play("c",sound,a);mixer.Play("d",sound,a);Check(mixer.Count==1,"exclusive group replaces voice");
                }
                using(var mixer=new Mixer()){
                    mixer.Master=1;var a=new ActionDef{Name="Vinheta",Pitch=false,Loop=true,Volume=100,FadeOut=0};mixer.Play("key",sound,a);var b=new float[1024];mixer.Read(b,0,b.Length);Check(b.Any(x=>Math.Abs(x)>0.01f),"playing key produces sound");
                    Check(mixer.Meter.Length==48&&mixer.Meter.Any(x=>x>0)&&mixer.NowPlaying=="Vinheta","waveform reflects real mixed audio and title");
                    mixer.SetVolume("key",0);mixer.Read(b,0,b.Length);Check(b.All(x=>x==0),"key volume updates currently playing sound");
                    mixer.StopAll();Check(mixer.Meter.All(x=>x==0)&&mixer.NowPlaying=="","waveform clears after stop all");
                }
                var root=Path.Combine(Path.GetTempPath(),"Mini32-tests-"+Guid.NewGuid().ToString("N"));var store=new Store(root);var settings=Settings.Create();store.Save(settings);settings.Banks[0].Name="Entrevista";store.Save(settings);Check(store.Load().Banks[0].Name=="Entrevista","atomic persistence roundtrip");File.WriteAllText(store.Config,"broken");Check(store.Load().Banks[0].Name=="Live principal"&&store.Recovery!="","corruption recovery from backup");
                var legacy=Settings.Create();legacy.Version=1;File.WriteAllText(store.Config,new JavaScriptSerializer().Serialize(legacy));var upgraded=store.Load();Check(upgraded.Version==2&&upgraded.Banks[0].Keys[0].Volume==null&&upgraded.Banks[0].Keys[0].IconFile=="","version 1 settings migrate without losing keys");
                upgraded.Banks[0].Keys[0].Volume=23;upgraded.Banks[0].Keys[0].IconFile="sample.png";store.Save(upgraded);Check(store.Load().Banks[0].Keys[0].Volume==23&&store.Load().Banks[0].Keys[0].IconFile=="sample.png","per-key volume and icon persist");
                upgraded.Banks[0].Keys[0].IconFile="../escape.png";bool badIcon=false;try{upgraded.Validate();}catch(InvalidDataException){badIcon=true;}Check(badIcon,"reject icon paths outside library");upgraded.Banks[0].Keys[0].IconFile="sample.png";
                upgraded.WallpaperFile="theme.png";store.Save(upgraded);Check(store.Load().WallpaperFile=="theme.png","wallpaper choice persists");
                upgraded.WallpaperFile="../escape.png";bool badWallpaper=false;try{upgraded.Validate();}catch(InvalidDataException){badWallpaper=true;}Check(badWallpaper,"reject wallpaper paths outside library");upgraded.WallpaperFile="theme.png";
                using(var engine=new Engine(new Store(Path.Combine(root,"volume-engine")))){
                    var sharedAction=new ActionDef{Volume=75};engine.Settings.Actions.Add(sharedAction);
                    var first=engine.Active.Keys[0];var second=engine.Active.Keys[1];first.ActionId=second.ActionId=sharedAction.Id;first.Volume=20;second.Volume=80;
                    Check(engine.ConfiguredAction(first).Volume==20&&engine.ConfiguredAction(second).Volume==80&&sharedAction.Volume==75,"shared audio action plays at each key's own volume");
                }
                var freeRoot=Path.Combine(root,"free-engine");var freeStore=new Store(freeRoot);var freeSettings=Settings.Create();
                var freeAction=new ActionDef{Kind="stop"};freeSettings.Actions.Add(freeAction);
                for(int i=0;i<17;i++)freeSettings.Banks[0].Keys[i].ActionId=freeAction.Id;
                var extraBank=Bank.Create("Segundo banco");freeSettings.Banks.Add(extraBank);freeStore.Save(freeSettings);
                using(var engine=new Engine(freeStore)){
                    Check(engine.Settings.FreeKeys.Count==16&&engine.KeyAvailable(0)&&!engine.KeyAvailable(16),"free selects 16 positions without deleting the 17th binding");
                    bool bankBlocked=false;try{engine.SetBank(extraBank.Id);}catch(InvalidOperationException){bankBlocked=true;}
                    Check(bankBlocked&&engine.Settings.Banks.Count==2,"free keeps additional banks but cannot use them");
                    engine.ChooseFreeKey(16,0);
                    Check(engine.KeyAvailable(16)&&!engine.KeyAvailable(0)&&engine.Active.Keys[0].ActionId==freeAction.Id,"replacing a free position preserves its action");
                }
                Check(freeStore.Load().FreeKeys.Contains(16)&&freeStore.Load().Banks.Count==2,"free selection and legacy banks survive restart");
                settings.Banks[0].Keys[1].Note=settings.Banks[0].Keys[0].Note;bool invalid=false;try{settings.Validate();}catch(InvalidDataException){invalid=true;}Check(invalid,"reject ambiguous MIDI mapping");
                Check(Store.Unprotect(Store.Protect("test-secret"))=="test-secret","Windows protected credentials roundtrip");
                string file=Path.Combine(root,"mono22050.wav");using(var writer=new WaveFileWriter(file,new WaveFormat(22050,16,1)))for(int i=0;i<22050;i++)writer.WriteSample((float)Math.Sin(2*Math.PI*440*i/22050)*0.1f);
                var decoded=Sound.Load(file);Check(Math.Abs(decoded.Seconds-1)<0.01,"mono 22050 converted to stereo 48000");
                string longFile=Path.Combine(root,"music-121s.wav");using(var writer=new WaveFileWriter(longFile,new WaveFormat(8000,16,1)))for(int i=0;i<121*8000;i++)writer.WriteSample((float)Math.Sin(2*Math.PI*220*i/8000)*0.1f);
                var music=Sound.Load(longFile);Check(music.Streaming&&Math.Abs(music.Seconds-121)<0.1,"long music uses bounded streaming path");
                using(var mixer=new Mixer()){
                    mixer.Master=1;mixer.Play("music",music,new ActionDef{Pitch=false,Volume=100,Loop=true,Music=true,FadeOut=0});
                    bool heard=false;var buffer=new float[1024];for(int i=0;i<200&&!heard;i++){mixer.Read(buffer,0,buffer.Length);heard=buffer.Any(x=>Math.Abs(x)>0.001f);if(!heard)Thread.Sleep(5);}
                    Check(heard&&mixer.Count==1,"long music streams audible samples without full decode");
                    mixer.Stop("music");mixer.Read(buffer,0,buffer.Length);Check(mixer.Count==0,"streamed music stops and releases voice");
                }
                Check(Engine.Sensitive("streamStart")&&Engine.Sensitive("streamStop")&&Engine.Sensitive("recordStop")&&!Engine.Sensitive("scene"),"sensitive action classification");
                Console.WriteLine("All engine tests passed.");return 0;
            }catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}
        }
    }
}
