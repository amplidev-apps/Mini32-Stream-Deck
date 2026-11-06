using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Mini32Diagnostic;
using NAudio.Wave;

namespace Mini32Lab
{
    internal static class Program
    {
        [STAThread] public static int Main(string[] args)
        {
            string root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".."));
            try {
                Directory.CreateDirectory(Path.Combine(root,"logs"));
                if(args.Length>0 && args[0]=="--self-test") {LabChecks.Run(root);return 0;}
                if(args.Length>0 && args[0]=="--device-test") {LabChecks.DeviceTest(root);return 0;}
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LabWindow(root,args.Length>0 && args[0]=="--ui-test"));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"startup-error.txt"),ex.ToString());if(args.Length==0)MessageBox.Show("Nao foi possivel iniciar. Consulte startup-error.txt.\n"+ex.Message,"Mini32");return 1;}
        }
    }
    public sealed class RunLog : IDisposable
    {
        private readonly StreamWriter writer;
        public string FilePath {get;private set;}
        public RunLog(string root) {
            FilePath=Path.Combine(root,"logs","bancada-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".jsonl");
            writer=new StreamWriter(FilePath,false,new System.Text.UTF8Encoding(false));
            Write("start",new {version=1,os=Environment.OSVersion.ToString()});
        }
        public void Write(string kind,object data) {writer.WriteLine(new JavaScriptSerializer().Serialize(new {utc=DateTimeOffset.UtcNow.ToString("o"),kind=kind,data=data}));writer.Flush();}
        public void Dispose(){Write("end",null);writer.Dispose();}
    }
    public sealed class OutputChoice
    {
        public int Index;public string Name;public override string ToString(){return Name;}
    }
    public sealed class LabWindow : Form
    {
        private readonly string root;
        private readonly RunLog log;
        private readonly AudioEngine engine=new AudioEngine();
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        private readonly ComboBox midiDevices=new ComboBox(),outputs=new ComboBox(),scenes=new ComboBox();
        private readonly Label midiState=new Label(),clipState=new Label(),obsState=new Label(),pitchState=new Label(),metrics=new Label();
        private readonly NumericUpDown audioNote=new NumericUpDown(),sceneNote=new NumericUpDown(),port=new NumericUpDown();
        private readonly TextBox password=new TextBox();
        private readonly CheckBox sceneArmed=new CheckBox(),hold=new CheckBox();
        private readonly TrackBar volume=new TrackBar();
        private readonly ListBox events=new ListBox();
        private readonly Button midiButton=new Button(),obsButton=new Button(),learnAudio=new Button(),learnScene=new Button();
        private MidiInput midi;
        private ObsClient obs;
        private AudioClip clip;
        private bool closing,loading,obsBusy;
        private int audioChannel=1,sceneChannel=1,learning,ticks;
        private readonly HashSet<int> down=new HashSet<int>();
        private readonly bool uiTest;
        private TimeSpan lastCpu;
        private DateTime lastMetrics=DateTime.UtcNow;
        private bool wasPlaying;
        public LabWindow(string root,bool uiTest)
        {
            this.root=root;this.uiTest=uiTest;log=new RunLog(root);
            Text="Mini32 Stream Deck - Bancada de audio e OBS";Size=new Size(1060,900);MinimumSize=new Size(920,800);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),ColumnCount=1,RowCount=6};Controls.Add(layout);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,108));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,230));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,200));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            layout.Controls.Add(new Label{Text="Bancada de testes: 1 tecla toca um som; outra troca uma cena. Mantenha a oitava do teste anterior.",Dock=DockStyle.Fill},0,0);
            var midiBox=Section("1. Conecte a controladora",layout,1);
            var midiRow=Row(midiBox);SetupCombo(midiDevices,300);midiRow.Controls.Add(midiDevices);
            Button(midiRow,"Atualizar entradas",delegate{RefreshMidi();});
            midiButton.Text="Conectar MIDI";midiButton.AutoSize=true;midiRow.Controls.Add(midiButton);midiButton.Click+=delegate{Safe(delegate{if(midi==null)ConnectMidi();else DisconnectMidi();});};
            midiState.Text="MIDI desconectado";midiState.AutoSize=true;midiBox.Controls.Add(midiState);

            var audioBox=Section("2. Escolha uma vinheta e uma tecla",layout,2);
            var fileRow=Row(audioBox);
            Button(fileRow,"Escolher WAV / MP3",ChooseClip);
            Button(fileRow,"Carregar som de exemplo",delegate{clip=LabChecks.MakeTone(3);clipState.Text="Exemplo de 3 segundos carregado. Clique Tocar ou pressione a tecla configurada.";log.Write("clip_loaded",new {name="example",seconds=3});});
            clipState.AutoSize=true;clipState.MaximumSize=new Size(900,0);clipState.Text="Nenhum som selecionado. Limite deste prototipo: 2 minutos, mono/estereo.";audioBox.Controls.Add(clipState);
            var bindRow=Row(audioBox);bindRow.Controls.Add(Caption("Nota MIDI do som:"));SetupNote(audioNote,53);bindRow.Controls.Add(audioNote);
            learnAudio.Text="Aprender tecla do som";learnAudio.AutoSize=true;bindRow.Controls.Add(learnAudio);learnAudio.Click+=delegate{Safe(delegate{BeginLearn(1);});};
            hold.Text="Parar ao soltar";hold.AutoSize=true;bindRow.Controls.Add(hold);
            var outRow=Row(audioBox);outRow.Controls.Add(Caption("Saida:"));SetupCombo(outputs,330);outRow.Controls.Add(outputs);RefreshOutputs();
            volume.Minimum=0;volume.Maximum=100;volume.Value=25;volume.TickFrequency=25;volume.Width=140;volume.Height=38;volume.ValueChanged+=delegate{engine.Gain=volume.Value/100f;};outRow.Controls.Add(Caption("Volume:"));outRow.Controls.Add(volume);
            var playRow=Row(audioBox);Button(playRow,"Tocar / reiniciar",delegate{PlayClip();});
            var stop=Button(playRow,"PARAR SOM",delegate{engine.Stop();log.Write("stop",new {source="button"});});stop.Font=new Font(Font,FontStyle.Bold);
            Button(playRow,"Pitch no centro",delegate{engine.PitchValue=8192;log.Write("pitch_reset",null);});
            pitchState.Text="Pitch: 0,0 semitons";pitchState.AutoSize=true;playRow.Controls.Add(pitchState);

            var obsBox=Section("3. Conecte ao OBS e escolha a cena",layout,3);
            var connectionRow=Row(obsBox);connectionRow.Controls.Add(Caption("Porta:"));port.Minimum=1;port.Maximum=65535;port.Value=4455;port.Width=80;connectionRow.Controls.Add(port);
            connectionRow.Controls.Add(Caption("Senha OBS:"));password.UseSystemPasswordChar=true;password.Width=210;connectionRow.Controls.Add(password);
            obsButton.Text="Conectar OBS";obsButton.AutoSize=true;connectionRow.Controls.Add(obsButton);obsButton.Click+=async delegate{await ConnectObs();};
            obsState.Text="No OBS: Ferramentas > Configuracoes do servidor WebSocket. Habilite e copie a senha aqui.";obsState.AutoSize=true;obsState.MaximumSize=new Size(920,0);obsBox.Controls.Add(obsState);
            var sceneRow=Row(obsBox);SetupCombo(scenes,300);sceneRow.Controls.Add(scenes);Button(sceneRow,"Atualizar cenas",async delegate{await RefreshScenes();});
            sceneRow.Controls.Add(Caption("Nota MIDI:"));SetupNote(sceneNote,54);sceneRow.Controls.Add(sceneNote);
            learnScene.Text="Aprender tecla da cena";learnScene.AutoSize=true;sceneRow.Controls.Add(learnScene);learnScene.Click+=delegate{Safe(delegate{BeginLearn(2);});};
            var actionRow=Row(obsBox);sceneArmed.Text="Habilitar troca de cena (altera a cena ao vivo do OBS)";sceneArmed.AutoSize=true;actionRow.Controls.Add(sceneArmed);
            Button(actionRow,"Testar troca de cena",async delegate{await ChangeScene();});

            events.Dock=DockStyle.Fill;events.HorizontalScrollbar=true;layout.Controls.Add(events,0,4);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};Button(footer,"Abrir logs",delegate{Process.Start(new ProcessStartInfo(Path.Combine(root,"logs")){UseShellExecute=true});});
            metrics.AutoSize=true;footer.Controls.Add(metrics);layout.Controls.Add(footer,0,5);
            RefreshMidi();timer.Interval=20;timer.Tick+=Tick;timer.Start();
            FormClosing+=OnClosing;Application.ThreadException+=OnUiException;
            Say("Pronto. Escolha o audio e conecte MIDI; OBS e opcional para testar o som.");
        }
        private static FlowLayoutPanel Section(string title,TableLayoutPanel layout,int index)
        {
            var box=new GroupBox{Text=title,Dock=DockStyle.Fill,Padding=new Padding(10,10,10,8)};
            var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};box.Controls.Add(flow);layout.Controls.Add(box,0,index);return flow;
        }
        private static FlowLayoutPanel Row(Control parent){var row=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=new Padding(0,2,0,2)};parent.Controls.Add(row);return row;}
        private static Label Caption(string text){return new Label{Text=text,AutoSize=true,Margin=new Padding(2,7,2,0)};}
        private static void SetupCombo(ComboBox box,int width){
            box.Width=width;box.DropDownStyle=ComboBoxStyle.DropDownList;box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=24;
            box.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();if(e.Index>=0)TextRenderer.DrawText(e.Graphics,Convert.ToString(box.Items[e.Index]),e.Font,e.Bounds,e.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
        }
        private static void SetupNote(NumericUpDown n,int value){n.Minimum=0;n.Maximum=127;n.Value=value;n.Width=60;}
        private Button Button(Control parent,string text,EventHandler handler){var b=new Button{Text=text,AutoSize=true};b.Click+=delegate(object s,EventArgs e){Safe(delegate{handler(s,e);});};parent.Controls.Add(b);return b;}
        private void Say(string text){if(closing)return;events.Items.Add(DateTime.Now.ToString("HH:mm:ss")+"  "+text);while(events.Items.Count>150)events.Items.RemoveAt(0);events.TopIndex=events.Items.Count-1;}
        private void Safe(Action action){try{action();}catch(Exception ex){Fail(ex);}}
        private void Fail(Exception ex){if(closing)return;Say("ERRO: "+ex.Message);try{log.Write("error",new {message=ex.Message});}catch{timer.Stop();engine.Stop();MessageBox.Show("Falha ao salvar o log. Feche a bancada e verifique espaco/permissoes da pasta.");}}
        private void OnUiException(object sender,System.Threading.ThreadExceptionEventArgs e){engine.Stop();Fail(e.Exception);}
        private void RefreshMidi(){if(midi!=null){Say("Desconecte MIDI antes de atualizar a lista.");return;}midiDevices.Items.Clear();foreach(var d in MidiInput.Devices())midiDevices.Items.Add(d);if(midiDevices.Items.Count>0)midiDevices.SelectedIndex=0;}
        private void RefreshOutputs(){outputs.Items.Add(new OutputChoice{Index=-1,Name="Saida padrao do Windows"});for(int i=0;i<WaveOut.DeviceCount;i++)outputs.Items.Add(new OutputChoice{Index=i,Name=WaveOut.GetCapabilities(i).ProductName});outputs.SelectedIndex=0;}
        private void ConnectMidi()
        {
            if(midiDevices.SelectedItem==null)throw new InvalidOperationException("Selecione uma entrada MIDI. Feche o monitor anterior se necessario.");
            var next=new MidiInput();try{next.Open((Device)midiDevices.SelectedItem);}catch{next.Dispose();throw;}
            midi=next;down.Clear();midiDevices.Enabled=false;midiButton.Text="Desconectar MIDI";midiState.Text="Conectado: "+midi.Device.Name;log.Write("midi_connected",midi.Device);Say("MIDI conectado. Som: nota "+audioNote.Value+"; cena: nota "+sceneNote.Value+".");
        }
        private void DisconnectMidi(){if(midi!=null){midi.Dispose();midi=null;}down.Clear();learning=0;midiDevices.Enabled=true;midiButton.Text="Conectar MIDI";midiState.Text="MIDI desconectado";}
        private void BeginLearn(int target){if(midi==null)throw new InvalidOperationException("Conecte MIDI primeiro.");learning=target;Say(target==1?"Pressione a tecla que tocara o som (nenhuma acao sera executada).":"Pressione a tecla que trocara a cena (nenhuma acao sera executada).");}
        private async void ChooseClip(object sender,EventArgs e)
        {
            if(loading)return;
            using(var picker=new OpenFileDialog{Filter="Audio WAV ou MP3|*.wav;*.mp3",Title="Escolha uma vinheta curta"}) {
                if(picker.ShowDialog(this)!=DialogResult.OK)return;
                loading=true;engine.Stop();clip=null;clipState.Text="Preparando audio...";string file=picker.FileName;
                try {var loaded=await Task.Run(delegate{return AudioClip.Load(file);});if(closing)return;clip=loaded;clipState.Text=clip.Name+" - "+clip.Seconds.ToString("0.0")+" s";log.Write("clip_loaded",new{name=clip.Name,seconds=clip.Seconds,rate=clip.Format.SampleRate,channels=clip.Format.Channels});}
                catch(Exception ex){if(!closing)clipState.Text="Nao foi possivel carregar o arquivo.";Fail(ex);}finally{loading=false;}
            }
        }
        private void PlayClip(){if(loading)throw new InvalidOperationException("Aguarde o carregamento.");if(clip==null)throw new InvalidOperationException("Escolha um audio ou carregue o exemplo primeiro.");engine.Play(clip,((OutputChoice)outputs.SelectedItem).Index);log.Write("play_requested",new{file=clip.Name,pitch=AudioEngine.ToSemitones(engine.PitchValue),volume=engine.Gain});Say("Tocando: "+clip.Name);}
        private async Task ConnectObs()
        {
            if(obsBusy || closing)return;obsBusy=true;obsButton.Enabled=false;
            try {
                if(obs!=null){obs.Dispose();obs=null;scenes.Items.Clear();sceneArmed.Checked=false;obsState.Text="OBS desconectado";obsButton.Text="Conectar OBS";return;}
                obsState.Text="Conectando ao OBS local...";var client=new ObsClient();obs=client;
                await client.Connect((int)port.Value,password.Text);password.Clear();if(closing)return;
                obsButton.Text="Desconectar OBS";log.Write("obs_connected",new{port=(int)port.Value});await FetchScenes();
            }catch(Exception ex){if(obs!=null)obs.Dispose();obs=null;if(!closing){obsState.Text="Sem conexao. Abra o OBS, habilite WebSocket e confira porta/senha.";obsButton.Text="Conectar OBS";}Fail(ex);}
            finally{obsBusy=false;if(!closing)obsButton.Enabled=true;}
        }
        private async Task FetchScenes()
        {
            if(obs==null || !obs.Connected)throw new InvalidOperationException("Conecte ao OBS primeiro.");
            var response=await obs.Request("GetSceneList",new{});if(closing)return;
            string selected=Convert.ToString(scenes.SelectedItem);scenes.Items.Clear();
            foreach(var item in (IEnumerable)response["scenes"])scenes.Items.Add(Convert.ToString(((Dictionary<string,object>)item)["sceneName"]));
            if(scenes.Items.Contains(selected))scenes.SelectedItem=selected;else if(scenes.Items.Count>0)scenes.SelectedIndex=0;
            obsState.Text="OBS conectado. Cena atual: "+Convert.ToString(response["currentProgramSceneName"]);
        }
        private async Task RefreshScenes(){if(obsBusy || closing)return;obsBusy=true;try{await FetchScenes();}catch(Exception ex){Fail(ex);}finally{obsBusy=false;}}
        private async Task ChangeScene()
        {
            if(closing)return;if(!sceneArmed.Checked){Say("Troca de cena desabilitada. Marque a opcao quando estiver pronto para testar.");return;}
            if(obsBusy){Say("OBS ocupado; comando descartado para nao executar atrasado.");return;}
            obsBusy=true;
            try {
                if(obs==null || !obs.Connected)throw new InvalidOperationException("Conecte ao OBS primeiro.");
                if(scenes.SelectedItem==null)throw new InvalidOperationException("Escolha uma cena.");
                string scene=Convert.ToString(scenes.SelectedItem);
                await obs.Request("SetCurrentProgramScene",new{sceneName=scene});if(closing)return;
                log.Write("scene_confirmed",new{scene=scene});Say("OBS confirmou a cena: "+scene);obsState.Text="OBS: cena solicitada confirmada - "+scene;
            }catch(Exception ex){Fail(ex);}finally{obsBusy=false;}
        }
        private void Tick(object sender,EventArgs args)
        {
            if(closing)return;
            try {
                ticks++;
                if(uiTest && ticks==15){log.Write("ui_test",new{midiItems=midiDevices.Items.Count,midiSelection=Convert.ToString(midiDevices.SelectedItem),outputItems=outputs.Items.Count,outputSelection=Convert.ToString(outputs.SelectedItem)});using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(root,"logs","ui-test.png"));}Close();return;}
                if(midi!=null)foreach(var e in midi.Drain()) {
                    if(Convert.ToString(e["event"])!="midi"){log.Write("midi_status",e);continue;}
                    string kind=Convert.ToString(e["kind"]);if(!e.ContainsKey("channel"))continue;int channel=Convert.ToInt32(e["channel"]);
                    if(kind=="PitchBend"){engine.PitchValue=Convert.ToInt32(e["value"]);continue;}
                    if(kind!="NoteOn" && kind!="NoteOff")continue;
                    int note=Convert.ToInt32(e["note"]),key=channel*128+note;log.Write("note",new{kind=kind,note=note,channel=channel});
                    if(kind=="NoteOff"){down.Remove(key);if(hold.Checked && note==(int)audioNote.Value && channel==audioChannel){engine.Stop();log.Write("stop",new{source="release"});}continue;}
                    if(!down.Add(key))continue;
                    if(learning!=0){
                        if(learning==1){if(note==(int)sceneNote.Value && channel==sceneChannel){Say("Essa tecla esta destinada a cena. Escolha outra.");continue;}audioNote.Value=note;audioChannel=channel;}
                        else{if(note==(int)audioNote.Value && channel==audioChannel){Say("Essa tecla esta destinada ao som. Escolha outra.");continue;}sceneNote.Value=note;sceneChannel=channel;}
                        Say("Tecla aprendida: nota "+note+", canal "+channel);learning=0;continue;
                    }
                    if(audioNote.Value==sceneNote.Value && audioChannel==sceneChannel){Say("Escolha notas diferentes para som e cena.");continue;}
                    if(note==(int)audioNote.Value && channel==audioChannel)Safe(PlayClip);
                    else if(note==(int)sceneNote.Value && channel==sceneChannel){var ignored=ChangeScene();}
                }
                pitchState.Text="Pitch: "+AudioEngine.ToSemitones(engine.PitchValue).ToString("+0.0;-0.0;0.0")+" semitons";
                bool playing=engine.IsPlaying;if(wasPlaying && !playing)log.Write("voice_ended",null);wasPlaying=playing;
                if(engine.OutputError!=null){string error=engine.OutputError;engine.Dispose();engine.OutputError=null;Fail(new IOException("Saida de audio: "+error));}
                if(ticks%50==0){using(var process=Process.GetCurrentProcess()){var now=DateTime.UtcNow;double cpu=(process.TotalProcessorTime-lastCpu).TotalMilliseconds/(now-lastMetrics).TotalMilliseconds/Environment.ProcessorCount*100;lastCpu=process.TotalProcessorTime;lastMetrics=now;metrics.Text="CPU aprox.: "+Math.Max(0,cpu).ToString("0.0")+"% | RAM: "+(process.WorkingSet64/1048576)+" MB | "+(playing?"Som tocando":"Som parado");}log.Write("status",new{pitch=engine.PitchValue,playing=playing});}
            }catch(Exception ex){engine.Stop();try{DisconnectMidi();}catch{}Fail(ex);}
        }
        private void OnClosing(object sender,FormClosingEventArgs args)
        {
            if(closing)return;
            timer.Stop();try{DisconnectMidi();}catch(Exception ex){args.Cancel=true;Fail(ex);timer.Start();return;}
            closing=true;engine.Dispose();if(obs!=null)obs.Dispose();timer.Dispose();Application.ThreadException-=OnUiException;log.Dispose();
        }
    }
}
