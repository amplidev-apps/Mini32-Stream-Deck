using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mini32Diagnostic;
using Mini32Lab;
using NAudio.Wave;

namespace Mini32
{
    public sealed class Press
    {
        public string VoiceId; public ActionDef Action; public DateTime At=DateTime.UtcNow; public bool Fired; public int Generation;
    }
    // All coordination runs on the host message loop. Only the mixer callback runs on an audio thread.
    public sealed class Engine : IDisposable
    {
        public readonly Store Store;public readonly Settings Settings;public readonly Mixer Mixer=new Mixer();public readonly Log Log;
        public readonly bool ProEnabled;
        public Action<string,object> Notify=delegate{};
        public bool UseMode,Disposed; public string MidiStatus="MIDI desconectado",ObsStatus="OBS desconectado",Last="Pronto para configurar suas teclas.";
        public string[] Scenes=new string[0],Inputs=new string[0];
        private MidiInput midi;private ObsClient obs;private bool connecting,obsBusy;private int ticks,generation,learning=-1;private string learnBank="";
        private readonly Dictionary<int,Press> presses=new Dictionary<int,Press>();
        private readonly HashSet<int> down=new HashSet<int>();
        private readonly Dictionary<string,Sound> cache=new Dictionary<string,Sound>();private readonly List<string> lru=new List<string>();
        private readonly Dictionary<string,Task<Sound>> loading=new Dictionary<string,Task<Sound>>();private long cachedBytes;
        private bool outputOpen;private DateTime nextMidi=DateTime.MinValue,nextObs=DateTime.MinValue;private string outputName="";
        public Engine(Store store,bool proEnabled=false){Store=store;Settings=store.Load();Settings.Validate();ProEnabled=proEnabled;if(!ProEnabled)Settings.ActiveBank=Settings.FreeBank;Log=new Log(store.Logs);Mixer.Master=Settings.Master/100f;Log.Write("start",new{version="0.3.0-dev.2",edition=ProEnabled?"pro-preview":"free"});if(store.Recovery!="")Last=store.Recovery;}
        public Bank Active{get{return Settings.Banks.First(b=>b.Id==Settings.ActiveBank);}}
        public ActionDef FindAction(Binding binding){return Settings.Actions.FirstOrDefault(a=>a.Id==binding.ActionId);}
        public ActionDef ConfiguredAction(Binding binding){var action=FindAction(binding);if(action==null)return null;var snapshot=action.Copy();snapshot.Id=action.Id;if(binding.Volume.HasValue)snapshot.Volume=binding.Volume.Value;return snapshot;}
        public void Say(string message){Last=message;Log.Write("message",message);Notify("status",Status());}
        public void Error(Exception e){if(!Disposed)Say(e.Message);}
        public object Status(){return new{midi=MidiStatus,obs=ObsStatus,midiConnected=midi!=null,obsConnected=obs!=null&&obs.Connected,playing=Mixer.Count,pitch=Mixer.Semitones(Mixer.PitchRaw),peak=Mixer.Peak,last=Last,useMode=UseMode,learning=learning,activeBank=Settings.ActiveBank,edition=ProEnabled?"pro":"free",pressed=down.ToArray()};}
        public object Devices(){return new{midi=MidiInput.Devices(),outputs=Enumerable.Range(0,WaveOut.DeviceCount).Select(i=>new{Index=i,Name=WaveOut.GetCapabilities(i).ProductName}).ToArray()};}
        public void Changed(){Store.Save(Settings);Notify("config",Settings);}
        public void SetMode(bool use){CancelPresses();learning=-1;UseMode=use;Say(use?"Modo Usar: teclas executam suas ações.":"Modo Editar: teclas selecionam a configuração.");}
        public void SetBank(string id){if(!Settings.Banks.Any(b=>b.Id==id))throw new InvalidOperationException("Banco não encontrado.");if(!ProEnabled&&id!=Settings.FreeBank)throw new InvalidOperationException("Bancos adicionais estarão disponíveis na edição Pro.");Settings.ActiveBank=id;learning=-1;Changed();Preload();}
        public bool KeyAvailable(int index){return index>=0&&index<32&&(ProEnabled||Settings.FreeKeys.Contains(index));}
        public void RequireKey(int index){if(index<0||index>=32)throw new InvalidOperationException("Tecla inválida.");if(!KeyAvailable(index))throw new InvalidOperationException("Escolha esta tecla entre as 16 posições da edição Free antes de configurá-la.");}
        public void ChooseFreeKey(int index,int replace)
        {
            if(ProEnabled)throw new InvalidOperationException("As 32 teclas já estão disponíveis na edição Pro.");
            if(index<0||index>=32||replace<0||replace>=32||!Settings.FreeKeys.Contains(replace))throw new InvalidOperationException("Escolha uma tecla ativa para substituir.");
            if(Settings.FreeKeys.Contains(index))return;
            CancelPresses();Mixer.Stop(Active.Id+":"+replace);
            Settings.FreeKeys.Remove(replace);Settings.FreeKeys.Add(index);Changed();Preload();
            Say("Tecla "+(index+1)+" ativada. A configuração da tecla "+(replace+1)+" foi preservada.");
        }
        public void Learn(int index){RequireKey(index);if(midi==null)throw new InvalidOperationException("Conecte a Mini 32 primeiro.");CancelPresses();learning=index;learnBank=Active.Id;UseMode=false;Say("Pressione a tecla física para aprender. Nenhuma ação será executada.");}
        public void ConnectMidi(string signature,bool remember)
        {
            DisconnectMidi();var device=MidiInput.Devices().FirstOrDefault(d=>d.Signature==signature);if(device==null)throw new InvalidOperationException("Mini 32 não encontrada. Confira o cabo USB.");
            var next=new MidiInput();try{next.Open(device);midi=next;}catch{next.Dispose();throw;}
            Settings.MidiSignature=signature;Settings.MidiAuto=remember;MidiStatus="MIDI: "+device.Name;Changed();Say("Mini 32 conectada.");
        }
        public void DisconnectMidi(){var old=midi;midi=null;if(old!=null)old.Dispose();down.Clear();CancelPresses();learning=-1;MidiStatus="MIDI desconectado";}
        public void StopAll(){generation++;presses.Clear();Mixer.StopAll();Say("Todos os sons e confirmações foram interrompidos.");}
        private void CancelPresses(){foreach(var p in presses.Values)if(p.Action!=null&&p.Action.Behavior=="hold")Mixer.Stop(p.VoiceId);presses.Clear();generation++;}
        public void Release(int token){down.Remove(token);Press p;if(presses.TryGetValue(token,out p)){presses.Remove(token);if(p.Action!=null&&p.Action.Behavior=="hold")Mixer.Stop(p.VoiceId);}}
        public static bool Sensitive(string kind){return kind=="streamStart"||kind=="streamStop"||kind=="recordStop";}
        public void PressKey(int index,int token,bool test)
        {
            if(!KeyAvailable(index)||presses.ContainsKey(token))return;
            var snapshot=ConfiguredAction(Active.Keys[index]);if(snapshot==null){Say("Esta tecla ainda está vazia.");return;}
            var press=new Press{VoiceId=Active.Id+":"+index,Action=snapshot,Generation=generation};presses[token]=press;
            if(Sensitive(snapshot.Kind)){Say("Mantenha a tecla pressionada por 1 segundo para confirmar: "+snapshot.Name);return;}
            press.Fired=true;var ignored=Execute(press,token);
        }
        private async Task Execute(Press p,int token)
        {
            try{
                var a=p.Action;
                if(a.Kind=="audio"){
                    if(a.Behavior=="toggle"&&Mixer.Playing(p.VoiceId)){Mixer.Stop(p.VoiceId);return;}
                    var asset=Settings.Assets.FirstOrDefault(s=>s.Id==a.AssetId);if(asset==null)throw new InvalidOperationException("Escolha um áudio para esta tecla.");
                    var sound=await Load(asset);
                    if(Disposed||p.Generation!=generation)return;
                    Press current;if(a.Behavior=="hold"&&(!presses.TryGetValue(token,out current)||current!=p))return;
                    EnsureOutput();Mixer.Play(p.VoiceId,sound,a);Say("Tocando: "+a.Name);
                }else if(a.Kind=="stop")StopAll();
                else if(a.Kind=="bank")SetBank(a.Target);
                else await ObsAction(a);
            }catch(Exception e){Error(e);}
        }
        private void EnsureOutput()
        {
            if(outputOpen)return;int device=-1;
            if(Settings.Output!=""){device=Enumerable.Range(0,WaveOut.DeviceCount).FirstOrDefault(i=>WaveOut.GetCapabilities(i).ProductName==Settings.Output);if(WaveOut.DeviceCount==0||WaveOut.GetCapabilities(device).ProductName!=Settings.Output)throw new InvalidOperationException("Saída de áudio não encontrada. Escolha outra em Conexões.");}
            Mixer.Open(device);outputOpen=true;outputName=Settings.Output;
        }
        public void SetOutput(string name){Mixer.Dispose();outputOpen=false;Settings.Output=name;Changed();Say("Saída selecionada. Os sons foram interrompidos.");}
        private async Task<Sound> Load(Asset asset)
        {
            Sound ready;if(cache.TryGetValue(asset.Id,out ready)){lru.Remove(asset.Id);lru.Add(asset.Id);return ready;}
            Task<Sound> task;if(loading.TryGetValue(asset.Id,out task))return await task;
            task=Task.Run(()=>Sound.Load(Store.AssetPath(asset)));loading[asset.Id]=task;
            try{ready=await task;if(Disposed)throw new ObjectDisposedException("Motor");long bytes=ready.Streaming?0:ready.Samples.LongLength*4;while(cachedBytes+bytes>128L*1024*1024&&lru.Count>0){string first=lru[0];lru.RemoveAt(0);cachedBytes-=cache[first].Streaming?0:cache[first].Samples.LongLength*4;cache.Remove(first);}cache[asset.Id]=ready;lru.Add(asset.Id);cachedBytes+=bytes;return ready;}finally{loading.Remove(asset.Id);}
        }
        public async void Preload(){foreach(var id in Active.Keys.Where((k,i)=>KeyAvailable(i)).Select(k=>FindAction(k)).Where(a=>a!=null&&a.Kind=="audio").Select(a=>a.AssetId).Distinct().ToArray()){var asset=Settings.Assets.FirstOrDefault(a=>a.Id==id);if(asset!=null)try{await Load(asset);}catch(Exception e){Error(e);}}}
        public async Task<Asset> Import(string path)
        {
            var sound=await Task.Run(()=>Sound.Load(path));if(Disposed)throw new ObjectDisposedException("Motor");
            var asset=new Asset{Name=Path.GetFileNameWithoutExtension(path),Seconds=sound.Seconds};asset.File=asset.Id+Path.GetExtension(path).ToLowerInvariant();
            File.Copy(path,Store.AssetPath(asset),false);Settings.Assets.Add(asset);Changed();return asset;
        }
        public async Task ConnectObs(int port,string password,bool remember)
        {
            if(connecting||obsBusy)throw new InvalidOperationException("Aguarde a conexão ou resposta do OBS.");connecting=true;
            var previous=obs;obs=null;if(previous!=null)previous.Dispose();var client=new ObsClient();obs=client;
            try{ObsStatus="Conectando ao OBS...";await client.Connect(port,password);if(Disposed||obs!=client)return;Settings.ObsPort=port;Settings.ObsAuto=remember;Settings.ObsSecret=remember?Store.Protect(password):"";await RefreshObs();Changed();Say("OBS conectado.");}
            catch{client.Dispose();if(obs==client)obs=null;ObsStatus="OBS desconectado — confira porta e senha";throw;}finally{connecting=false;nextObs=DateTime.UtcNow.AddSeconds(10);}
        }
        public void DisconnectObs(){Settings.ObsAuto=false;if(obs!=null)obs.Dispose();obs=null;ObsStatus="OBS desconectado";Changed();}
        public async Task RefreshObs()
        {
            var client=obs;if(client==null||!client.Connected)throw new InvalidOperationException("Conecte ao OBS primeiro.");
            if(obsBusy)throw new InvalidOperationException("Aguarde a resposta do OBS.");obsBusy=true;
            try{var scenes=await client.Request("GetSceneList",new{});var inputs=await client.Request("GetInputList",new{});if(Disposed||client!=obs)return;
                Scenes=((IEnumerable)scenes["scenes"]).Cast<Dictionary<string,object>>().Select(s=>Convert.ToString(s["sceneName"])).ToArray();
                Inputs=((IEnumerable)inputs["inputs"]).Cast<Dictionary<string,object>>().Select(s=>Convert.ToString(s["inputName"])).ToArray();
                ObsStatus="OBS · "+Convert.ToString(scenes["currentProgramSceneName"]);Notify("targets",new{scenes=Scenes,inputs=Inputs});
            }finally{obsBusy=false;}
        }
        private async Task ObsAction(ActionDef a)
        {
            if(obsBusy||connecting)throw new InvalidOperationException("OBS ocupado; comando descartado para não executar com atraso.");
            var client=obs;if(client==null||!client.Connected)throw new InvalidOperationException("OBS desconectado. Abra Conexões.");
            obsBusy=true;
            try{string request;object data=new{};
                switch(a.Kind){case "scene":request="SetCurrentProgramScene";data=new{sceneName=a.Target};break;
                    case "preview":request="SetCurrentPreviewScene";data=new{sceneName=a.Target};break;
                    case "transition":request="TriggerStudioModeTransition";break;
                    case "mute":request="ToggleInputMute";data=new{inputName=a.Target};break;
                    case "recordStart":request="StartRecord";break;case "recordStop":request="StopRecord";break;
                    case "streamStart":request="StartStream";break;case "streamStop":request="StopStream";break;
                    default:throw new InvalidOperationException("Ação não suportada.");}
                await client.Request(request,data);if(!Disposed){if(a.Kind=="scene")ObsStatus="OBS · "+a.Target;Say("OBS confirmou: "+a.Name);Log.Write("obs_confirmed",new{request=request,target=a.Target});}
            }finally{obsBusy=false;}
        }
        public async void Tick()
        {
            if(Disposed)return;
            try{
                ticks++;
                if(midi!=null)foreach(var e in midi.Drain()){
                    if(Convert.ToString(e["event"])=="capture_loss"){CancelPresses();down.Clear();Say("A entrada MIDI perdeu eventos. Confirmações e teclas mantidas foram canceladas.");continue;}
                    if(Convert.ToString(e["event"])!="midi")continue;string kind=Convert.ToString(e["kind"]);
                    if(kind=="PitchBend"){Mixer.PitchRaw=Convert.ToInt32(e["value"]);continue;}
                    if(kind!="NoteOn"&&kind!="NoteOff")continue;int note=Convert.ToInt32(e["note"]),channel=Convert.ToInt32(e["channel"]),token=channel*128+note;
                    if(kind=="NoteOff"){Release(token);continue;}if(!down.Add(token))continue;
                    if(learning>=0){var b=Settings.Banks.First(x=>x.Id==learnBank);if(b.Keys.Where((k,i)=>i!=learning).Any(k=>k.Note==note&&k.Channel==channel)){Say("Essa nota já está em outra tecla deste banco. Ajuste a oitava ou escolha outra tecla.");continue;}b.Keys[learning].Note=note;b.Keys[learning].Channel=channel;learning=-1;Changed();Say("Tecla aprendida.");continue;}
                    int index=Active.Keys.FindIndex(k=>k.Note==note&&k.Channel==channel);if(index<0){Say("Nota sem configuração neste banco. Confira a oitava da Mini 32.");continue;}
                    if(!UseMode){Notify("select",new{index=index});continue;}PressKey(index,token,false);
                }
                foreach(var entry in presses.ToArray()){var p=entry.Value;if(!p.Fired&&Sensitive(p.Action.Kind)&&(DateTime.UtcNow-p.At).TotalSeconds>=1){p.Fired=true;var ignored=Execute(p,entry.Key);}}
                if(Mixer.Error!=null){string error=Mixer.Error;Mixer.Dispose();outputOpen=false;Mixer.Error=null;Say("Falha na saída de áudio: "+error);}
                if(Mixer.StreamError!=null){string error=Mixer.StreamError;Mixer.StreamError=null;Say("Não foi possível ler uma música da biblioteca: "+error);}
                if(ticks%5==0)Notify("meter",new{bars=Mixer.Meter,title=Mixer.NowPlaying,playing=Mixer.Count});
                if(ticks%20==0)Notify("status",Status());
                if(ticks%200!=0)return;
                if(midi!=null&&!MidiInput.Devices().Any(d=>d.Signature==midi.Device.Signature)){DisconnectMidi();Say("Mini 32 removida. Aguardando reconexão.");}
                if(midi==null&&Settings.MidiAuto&&DateTime.UtcNow>=nextMidi){nextMidi=DateTime.UtcNow.AddSeconds(5);try{ConnectMidi(Settings.MidiSignature,true);}catch{MidiStatus="Aguardando Mini 32 · feche outros monitores MIDI";}}
                if(outputOpen&&outputName!=""&&!Enumerable.Range(0,WaveOut.DeviceCount).Any(i=>WaveOut.GetCapabilities(i).ProductName==outputName)){Mixer.Dispose();outputOpen=false;Say("Saída de áudio removida. Reconecte e escolha a saída em Conexões.");}
                if(obs!=null&&!obs.Connected&&!connecting)ObsStatus="OBS desconectado";
                if(!connecting&&!obsBusy&&Settings.ObsAuto&&(obs==null||!obs.Connected)&&DateTime.UtcNow>=nextObs){nextObs=DateTime.UtcNow.AddSeconds(10);try{await ConnectObs(Settings.ObsPort,Store.Unprotect(Settings.ObsSecret),true);}catch{ObsStatus="Aguardando OBS · confira as configurações";}}
            }catch(Exception e){Error(e);}
        }
        public void Dispose(){if(Disposed)return;Disposed=true;generation++;try{DisconnectMidi();}finally{Mixer.Dispose();if(obs!=null)obs.Dispose();Log.Write("exit",null);Log.Dispose();}}
    }
}
