using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Mini32
{
    public sealed class Host : ApplicationContext
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int kind);
        private readonly Control dispatcher=new Control();private readonly Engine engine;
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        private readonly NotifyIcon tray;private readonly object writeLock=new object();private bool exiting,importing;
        private readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=4000000};
        private readonly int ownerPid;
        private sealed class Owner : IWin32Window {public IntPtr Handle{get;set;}}
        public Host(string root,int ownerPid)
        {
            this.ownerPid=ownerPid;
            dispatcher.CreateControl();var handle=dispatcher.Handle;
            engine=new Engine(new Store(root));engine.Notify=Send;
            tray=new NotifyIcon{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath),Text="Mini32 Stream Deck",Visible=true};
            var menu=new ContextMenuStrip();menu.Items.Add("Abrir Mini32 Stream Deck",null,delegate{Send("show",null);});menu.Items.Add("Parar todos os sons",null,delegate{engine.StopAll();});menu.Items.Add("Sair",null,delegate{Send("exit",null);Shutdown();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Send("show",null);};
            timer.Interval=10;timer.Tick+=delegate{engine.Tick();};timer.Start();
            Send("ready",new{protocol=1,version="0.3.0-dev.2",root=root});Send("config",engine.Settings);Send("devices",engine.Devices());Send("status",engine.Status());engine.Preload();
            // Read the inherited pipe directly: .NET Framework Console input stalls
            // with Dart's Windows child-process pipes. Do not own the standard handle.
            Task.Run(delegate{try{using(var stream=new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(GetStdHandle(-10),false),FileAccess.Read,4096,false))using(var input=new StreamReader(stream,new UTF8Encoding(false),false)){string line;while((line=input.ReadLine())!=null){if(line.Length>200000)continue;string command=line;dispatcher.BeginInvoke(new Action(async delegate{await Command(command);}));}}}catch(Exception e){Console.Error.WriteLine(e.Message);}finally{try{dispatcher.BeginInvoke(new Action(Shutdown));}catch{}}});
        }
        private void Send(string kind,object data){lock(writeLock){if(exiting)return;try{Console.WriteLine(new JavaScriptSerializer{MaxJsonLength=4000000}.Serialize(new{type=kind,data=data}));Console.Out.Flush();}catch(IOException){dispatcher.BeginInvoke(new Action(Shutdown));}}}
        private static string Text(Dictionary<string,object> d,string key,string fallback){return d.ContainsKey(key)?Convert.ToString(d[key]):fallback;}
        private static int Number(Dictionary<string,object> d,string key,int fallback){return d.ContainsKey(key)?Convert.ToInt32(d[key]):fallback;}
        private static bool Flag(Dictionary<string,object> d,string key){return d.ContainsKey(key)&&Convert.ToBoolean(d[key]);}
        private async Task Command(string line)
        {
            int id=0;
            try{
                var d=json.Deserialize<Dictionary<string,object>>(line);id=Number(d,"id",0);string op=Text(d,"op","");object result=null;
                switch(op){
                    case "hello":Send("config",engine.Settings);Send("devices",engine.Devices());Send("status",engine.Status());break;
                    case "devices":Send("devices",engine.Devices());break;
                    case "mode":engine.SetMode(Flag(d,"use"));break;
                    case "press":engine.PressKey(Number(d,"index",0),Number(d,"token",-1),true);break;
                    case "release":engine.Release(Number(d,"token",-1));break;
                    case "stop":engine.StopAll();break;
                    case "learn":engine.Learn(Number(d,"index",0));break;
                    case "midi":engine.ConnectMidi(Text(d,"signature",""),Flag(d,"remember"));break;
                    case "midiOff":engine.Settings.MidiAuto=false;engine.DisconnectMidi();engine.Changed();break;
                    case "output":engine.SetOutput(Text(d,"name",""));break;
                    case "master":engine.Settings.Master=Math.Max(0,Math.Min(100,Number(d,"value",70)));engine.Mixer.Master=engine.Settings.Master/100f;engine.Changed();break;
                    case "obs":await engine.ConnectObs(Number(d,"port",4455),Text(d,"password",""),Flag(d,"remember"));break;
                    case "obsOff":engine.DisconnectObs();break;
                    case "obsRefresh":await engine.RefreshObs();break;
                    case "bank":engine.SetBank(Text(d,"bank",""));break;
                    case "freeKey":engine.ChooseFreeKey(Number(d,"index",-1),Number(d,"replace",-1));break;
                    case "newBank":{if(!engine.ProEnabled)throw new InvalidOperationException("Bancos adicionais estarão disponíveis na edição Pro.");if(engine.Settings.Banks.Count>=100)throw new InvalidOperationException("Limite de 100 bancos.");var b=Bank.Create(Text(d,"name","Novo banco"));engine.Settings.Banks.Add(b);engine.SetBank(b.Id);break;}
                    case "renameBank":engine.Active.Name=Text(d,"name","Banco");engine.Changed();break;
                    case "action":{
                        int index=Number(d,"index",0);engine.RequireKey(index);
                        var a=json.Deserialize<ActionDef>(json.Serialize(d["action"]));
                        var candidate=json.Deserialize<Settings>(json.Serialize(engine.Settings));
                        if(!new[]{"audio","scene","preview","transition","mute","recordStart","recordStop","streamStart","streamStop","stop","bank"}.Contains(a.Kind))throw new InvalidOperationException("Ação desconhecida.");
                        if(!new[]{"trigger","hold","toggle"}.Contains(a.Behavior))throw new InvalidOperationException("Comportamento desconhecido.");
                        if(a.Kind=="audio"){var asset=candidate.Assets.FirstOrDefault(x=>x.Id==a.AssetId);if(asset==null)throw new InvalidOperationException("Importe e selecione um áudio.");if(a.Start>=asset.Seconds||(a.End>0&&(a.End<=a.Start||a.End>asset.Seconds+0.01)))throw new InvalidOperationException("Confira o início e fim do recorte.");}
                        if(String.IsNullOrWhiteSpace(a.Name))a.Name="Tecla "+(index+1);a.Name=a.Name.Substring(0,Math.Min(80,a.Name.Length));
                        string previous=engine.Active.Keys[index].ActionId;
                        if(Flag(d,"shared")&&previous!="")a.Id=previous;else a.Id=Guid.NewGuid().ToString("N");
                        candidate.Actions.RemoveAll(x=>x.Id==a.Id);candidate.Actions.Add(a);var key=candidate.Banks.First(x=>x.Id==candidate.ActiveBank).Keys[index];key.ActionId=a.Id;
                        key.Volume=d.ContainsKey("volume")?(int?)Number(d,"volume",75):(Flag(d,"shared")?key.Volume:null);
                        var used=new HashSet<string>(candidate.Banks.SelectMany(b=>b.Keys.Select(k=>k.ActionId)));candidate.Actions.RemoveAll(x=>!used.Contains(x.Id));candidate.Validate();
                        engine.Settings.Actions=candidate.Actions;engine.Active.Keys[index].ActionId=a.Id;engine.Active.Keys[index].Volume=key.Volume;engine.Mixer.SetVolume(engine.Active.Id+":"+index,key.Volume.HasValue?key.Volume.Value:a.Volume);engine.Changed();engine.Preload();break;
                    }
                    case "share":{int index=Number(d,"index",0);string action=Text(d,"actionId","");engine.RequireKey(index);if(!engine.Settings.Actions.Any(a=>a.Id==action))throw new InvalidOperationException("Ação não encontrada.");engine.Active.Keys[index].ActionId=action;engine.Changed();engine.Preload();break;}
                    case "clear":{int index=Number(d,"index",0);engine.RequireKey(index);var key=engine.Active.Keys[index];key.ActionId="";key.Volume=null;key.IconFile="";engine.Changed();break;}
                    case "keyVolume":{int index=Number(d,"index",0),volume=Number(d,"volume",75);engine.RequireKey(index);if(volume<0||volume>100)throw new InvalidOperationException("Volume da tecla inválido.");engine.Active.Keys[index].Volume=volume;engine.Mixer.SetVolume(engine.Active.Id+":"+index,volume);engine.Changed();break;}
                    case "removeIcon":{int index=Number(d,"index",0);engine.RequireKey(index);engine.Active.Keys[index].IconFile="";engine.Changed();break;}
                    case "removeWallpaper":engine.Settings.WallpaperFile="";engine.Changed();break;
                    case "wallpaper":{
                        using(var dialog=new OpenFileDialog{Filter="Imagem PNG ou JPEG|*.png;*.jpg;*.jpeg",Title="Escolher wallpaper do aplicativo"}){
                            IntPtr owner=IntPtr.Zero;try{if(ownerPid>0)owner=Process.GetProcessById(ownerPid).MainWindowHandle;}catch{}
                            var choice=owner==IntPtr.Zero?dialog.ShowDialog():dialog.ShowDialog(new Owner{Handle=owner});
                            if(choice==DialogResult.OK){
                                var source=new FileInfo(dialog.FileName);if(source.Length>15*1024*1024)throw new InvalidOperationException("Escolha uma imagem de até 15 MB.");
                                using(var image=Image.FromFile(source.FullName))if(image.Width<1||image.Height<1||image.Width>8192||image.Height>8192)throw new InvalidOperationException("Imagem inválida ou acima de 8192 pixels.");
                                string file=Guid.NewGuid().ToString("N")+source.Extension.ToLowerInvariant();File.Copy(source.FullName,Path.Combine(engine.Store.Wallpapers,file),false);
                                engine.Settings.WallpaperFile=file;engine.Changed();
                            }
                        }break;
                    }
                    case "icon":{
                        int index=Number(d,"index",0);engine.RequireKey(index);
                        using(var dialog=new OpenFileDialog{Filter="Imagem PNG ou JPEG|*.png;*.jpg;*.jpeg",Title="Escolher imagem da tecla"}){
                            IntPtr owner=IntPtr.Zero;try{if(ownerPid>0)owner=Process.GetProcessById(ownerPid).MainWindowHandle;}catch{}
                            var choice=owner==IntPtr.Zero?dialog.ShowDialog():dialog.ShowDialog(new Owner{Handle=owner});
                            if(choice==DialogResult.OK){
                                var source=new FileInfo(dialog.FileName);if(source.Length>5*1024*1024)throw new InvalidOperationException("Escolha uma imagem de até 5 MB.");
                                using(var image=Image.FromFile(source.FullName))if(image.Width<1||image.Height<1||image.Width>4096||image.Height>4096)throw new InvalidOperationException("Imagem inválida ou acima de 4096 pixels.");
                                string file=Guid.NewGuid().ToString("N")+source.Extension.ToLowerInvariant();File.Copy(source.FullName,Path.Combine(engine.Store.Icons,file),false);
                                engine.Active.Keys[index].IconFile=file;engine.Changed();
                            }
                        }break;
                    }
                    case "import":{
                        if(importing)throw new InvalidOperationException("Já existe uma importação em andamento.");importing=true;
                        try{using(var dialog=new OpenFileDialog{Filter="Áudio WAV / MP3|*.wav;*.mp3",Multiselect=true,Title="Importar vinhetas para Mini32 Stream Deck"}){
                            IntPtr owner=IntPtr.Zero;try{if(ownerPid>0)owner=Process.GetProcessById(ownerPid).MainWindowHandle;}catch{}
                            var resultDialog=owner==IntPtr.Zero?dialog.ShowDialog():dialog.ShowDialog(new Owner{Handle=owner});
                            if(resultDialog==DialogResult.OK){foreach(string file in dialog.FileNames)await engine.Import(file);engine.Say("Áudios importados para sua biblioteca.");}
                        }}finally{importing=false;}break;
                    }
                    case "example":{
                        string sample=Path.Combine(engine.Store.Root,"som-exemplo.wav");
                        if(!File.Exists(sample)){using(var wav=new NAudio.Wave.WaveFileWriter(sample,new NAudio.Wave.WaveFormat(48000,16,1))){for(int i=0;i<48000;i++){float envelope=(float)Math.Min(1,Math.Min(i/960.0,(48000-i)/960.0));wav.WriteSample((float)Math.Sin(2*Math.PI*440*i/48000)*0.15f*envelope);}}}
                        result=await engine.Import(sample);break;
                    }
                    case "openData":Process.Start(new ProcessStartInfo(engine.Store.Root){UseShellExecute=true});break;
                    case "exit":Send("reply",new{id=id,ok=true});Shutdown();return;
                    default:throw new InvalidOperationException("Comando desconhecido: "+op);
                }
                Send("reply",new{id=id,ok=true,result=result});
            }catch(Exception e){engine.Error(e);Send("reply",new{id=id,ok=false,error=e.Message});}
        }
        private void Shutdown(){if(exiting)return;exiting=true;timer.Stop();tray.Visible=false;tray.Dispose();try{engine.Dispose();}catch{}dispatcher.Dispose();ExitThread();}
    }
    public static class Program
    {
        [STAThread] public static int Main(string[] args)
        {
            Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
            if(args.Contains("--test"))return Tests.Run();
            string mutexName=args.Contains("--data-dir")?"Local\\Mini32StreamDeck.Engine.Isolated."+Guid.NewGuid().ToString("N"):"Local\\Mini32StreamDeck.Engine.v1";
            bool created;using(var mutex=new Mutex(true,mutexName,out created)){
                if(!created){Console.WriteLine("{\"type\":\"fatal\",\"data\":\"O Mini32 Stream Deck já está aberto. Procure o ícone na bandeja do Windows.\"}");return 2;}
                try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Mini32 Stream Deck");int rootArg=Array.IndexOf(args,"--data-dir");if(rootArg>=0&&rootArg+1<args.Length)root=Path.GetFullPath(args[rootArg+1]);int owner=0,ownerArg=Array.IndexOf(args,"--owner-pid");if(ownerArg>=0&&ownerArg+1<args.Length)Int32.TryParse(args[ownerArg+1],out owner);Application.Run(new Host(root,owner));return 0;}
                catch(Exception e){Console.WriteLine(new JavaScriptSerializer().Serialize(new{type="fatal",data=e.Message}));return 1;}
            }
        }
    }
}
