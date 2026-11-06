using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Mini32Diagnostic
{
    public sealed class Device
    {
        public uint Index { get; set; }
        public string Name { get; set; }
        public ushort Manufacturer { get; set; }
        public ushort Product { get; set; }
        public override string ToString() { return Index + " - " + Name; }
        public string Signature { get { return Name + "|" + Manufacturer + "|" + Product; } }
    }

    public static class Decode
    {
        public static Dictionary<string, object> Short(uint packed)
        {
            int status = (int)(packed & 255), a = (int)((packed >> 8) & 255), b = (int)((packed >> 16) & 255);
            int family = status & 240;
            string kind = "System";
            int length = 1;
            if (status >= 128 && status < 240) {
                length = family == 192 || family == 208 ? 2 : 3;
                switch (family) {
                    case 128: kind = "NoteOff"; break;
                    case 144: kind = b == 0 ? "NoteOff" : "NoteOn"; break;
                    case 160: kind = "PolyPressure"; break;
                    case 176: kind = "ControlChange"; break;
                    case 192: kind = "ProgramChange"; break;
                    case 208: kind = "ChannelPressure"; break;
                    case 224: kind = "PitchBend"; break;
                }
            } else if (status == 241 || status == 243) length = 2;
            else if (status == 242) length = 3;
            string hex = status.ToString("X2") + (length > 1 ? " " + a.ToString("X2") : "") + (length > 2 ? " " + b.ToString("X2") : "");
            var d = new Dictionary<string, object> { {"kind",kind}, {"raw_hex",hex}, {"packed",packed}, {"status",status} };
            if (status >= 128 && status < 240) d["channel"] = (status & 15) + 1;
            if (length > 1) d["data1"] = a;
            if (length > 2) d["data2"] = b;
            if (family == 128 || family == 144 || family == 160) d["note"] = a;
            if (family == 128 || family == 144) { d["velocity"] = b; d["zero_velocity_note_on"] = family == 144 && b == 0; }
            if (family == 176) { d["controller"] = a; d["value"] = b; }
            if (family == 224) { int value = (b << 7) | a; d["value"] = value; d["centered_value"] = value - 8192; }
            return d;
        }
    }

    public sealed class MidiInput : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        private struct Caps {
            public ushort manufacturer, product;
            public uint version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string name;
            public uint support;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Header {
            public IntPtr data;
            public uint length, recorded;
            public UIntPtr user;
            public uint flags;
            public IntPtr next;
            public UIntPtr reserved;
            public uint offset;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=8)] public UIntPtr[] extra;
        }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void Callback(IntPtr handle, uint message, UIntPtr instance, UIntPtr p1, UIntPtr p2);
        [DllImport("winmm.dll")] private static extern uint midiInGetNumDevs();
        [DllImport("winmm.dll", CharSet=CharSet.Unicode)] private static extern uint midiInGetDevCapsW(UIntPtr id, out Caps caps, uint size);
        [DllImport("winmm.dll")] private static extern uint midiInOpen(out IntPtr handle, uint device, Callback callback, UIntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint midiInStart(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInStop(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInClose(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint midiInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint midiInAddBuffer(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll", CharSet=CharSet.Unicode)] private static extern uint midiInGetErrorTextW(uint code, StringBuilder text, uint length);

        private readonly object gate = new object();
        private readonly Queue<Dictionary<string,object>> events = new Queue<Dictionary<string,object>>();
        private readonly Queue<IntPtr> returned = new Queue<IntPtr>();
        private readonly List<IntPtr> buffers = new List<IntPtr>();
        private readonly Callback callback;
        private IntPtr handle;
        private long sequence;
        private int dropped;
        private string stage = "livre";
        private bool closing;
        public Device Device { get; private set; }
        public MidiInput() { callback = Receive; }

        public static Device[] Devices()
        {
            var result = new List<Device>();
            uint n = midiInGetNumDevs();
            for (uint i=0; i<n; i++) {
                Caps caps;
                uint error = midiInGetDevCapsW(new UIntPtr(i), out caps, (uint)Marshal.SizeOf(typeof(Caps)));
                if (error == 0) result.Add(new Device { Index=i, Name=caps.name, Manufacturer=caps.manufacturer, Product=caps.product });
            }
            return result.ToArray();
        }
        private static void Check(uint result, string operation)
        {
            if (result == 0) return;
            var text = new StringBuilder(256);
            midiInGetErrorTextW(result, text, 256);
            throw new InvalidOperationException(operation + " (" + result + "): " + text);
        }
        public void Open(Device device)
        {
            if (handle != IntPtr.Zero) throw new InvalidOperationException("Entrada ja aberta.");
            Device = device;
            Check(midiInOpen(out handle, device.Index, callback, UIntPtr.Zero, 0x30020), "Abrir MIDI");
            try {
                for (int i=0; i<4; i++) {
                    var header = new Header { data=Marshal.AllocHGlobal(4096), length=4096, extra=new UIntPtr[8] };
                    IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Header)));
                    Marshal.StructureToPtr(header, ptr, false);
                    buffers.Add(ptr);
                    Check(midiInPrepareHeader(handle, ptr, (uint)Marshal.SizeOf(typeof(Header))), "Preparar SysEx");
                    Check(midiInAddBuffer(handle, ptr, (uint)Marshal.SizeOf(typeof(Header))), "Receber SysEx");
                }
                Check(midiInStart(handle), "Iniciar MIDI");
                Mark("connection", "Entrada aberta: " + device.Name);
            } catch { Dispose(); throw; }
        }
        private void Enqueue(Dictionary<string,object> entry)
        {
            // Called under gate: markers and MIDI receive one ordered timeline and stage identity.
            entry["sequence"] = ++sequence;
            entry["utc"] = DateTimeOffset.UtcNow.ToString("o");
            entry["stage"] = stage;
            if (events.Count >= 20000) { dropped++; return; }
            events.Enqueue(entry);
        }
        public void SetStage(string id, string instruction)
        {
            lock(gate) { stage=id; Enqueue(new Dictionary<string,object> { {"event","stage"}, {"instruction",instruction} }); }
        }
        public void Mark(string type, string text)
        {
            lock(gate) Enqueue(new Dictionary<string,object> { {"event",type}, {"text",text} });
        }
        private void Receive(IntPtr source, uint message, UIntPtr instance, UIntPtr p1, UIntPtr p2)
        {
            // No WinMM calls or file/UI work from the native callback.
            try {
                lock(gate) {
                    if (message == 0x3C3 || message == 0x3C5 || message == 0x3CC) {
                        var entry = Decode.Short(unchecked((uint)p1.ToUInt64()));
                        entry["event"] = message == 0x3C5 ? "midi_error" : "midi";
                        entry["driver_ms"] = p2.ToUInt64();
                        entry["driver_overload"] = message == 0x3CC;
                        Enqueue(entry);
                    } else if (message == 0x3C4 || message == 0x3C6) {
                        IntPtr ptr = new IntPtr(unchecked((long)p1.ToUInt64()));
                        Header h = (Header)Marshal.PtrToStructure(ptr, typeof(Header));
                        int count = (int)Math.Min(h.recorded,h.length);
                        if (count > 0) {
                            byte[] bytes = new byte[count]; Marshal.Copy(h.data,bytes,0,count);
                            Enqueue(new Dictionary<string,object> { {"event",message == 0x3C6 ? "midi_error" : "midi"}, {"kind","SysExChunk"}, {"raw_hex",BitConverter.ToString(bytes).Replace('-', ' ')}, {"driver_ms",p2.ToUInt64()}, {"bytes",count} });
                        }
                        if (!closing) returned.Enqueue(ptr);
                    }
                }
            } catch { Interlocked.Increment(ref dropped); }
        }
        public List<Dictionary<string,object>> Drain()
        {
            var result = new List<Dictionary<string,object>>();
            lock(gate) {
                int lost=Interlocked.Exchange(ref dropped,0);
                while(events.Count>0) result.Add(events.Dequeue());
                if (lost>0) {
                    Enqueue(new Dictionary<string,object> { {"event","capture_loss"}, {"count",lost} });
                    result.Add(events.Dequeue());
                }
            }
            // Requeue returned SysEx buffers only outside the callback and its lock.
            while(!closing && handle != IntPtr.Zero) {
                IntPtr ptr;
                lock(gate) { if(returned.Count==0) break; ptr=returned.Dequeue(); }
                Check(midiInAddBuffer(handle,ptr,(uint)Marshal.SizeOf(typeof(Header))),"Rearmar SysEx");
            }
            return result;
        }
        public void Dispose()
        {
            if(handle==IntPtr.Zero) return;
            lock(gate) closing=true;
            midiInStop(handle);
            Check(midiInReset(handle),"Liberar buffers MIDI");
            foreach(IntPtr ptr in buffers) {
                Check(midiInUnprepareHeader(handle,ptr,(uint)Marshal.SizeOf(typeof(Header))),"Liberar SysEx");
            }
            Check(midiInClose(handle),"Fechar MIDI");
            handle=IntPtr.Zero;
            foreach(IntPtr ptr in buffers) {
                Header h=(Header)Marshal.PtrToStructure(ptr,typeof(Header));
                Marshal.FreeHGlobal(h.data); Marshal.FreeHGlobal(ptr);
            }
            buffers.Clear();
            GC.KeepAlive(callback);
        }
    }

    public sealed class SessionLog : IDisposable
    {
        private readonly StreamWriter writer;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private readonly string summaryPath;
        private long events, losses;
        private readonly Dictionary<string,int> counts = new Dictionary<string,int>();
        public string Path { get; private set; }
        public SessionLog(string root, Device device, string prefix)
        {
            string directory=System.IO.Path.Combine(root,"logs"); Directory.CreateDirectory(directory);
            string name=prefix+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
            Path=System.IO.Path.Combine(directory,name+".jsonl");
            summaryPath=System.IO.Path.Combine(directory,name+"-resumo.txt");
            writer=new StreamWriter(Path,false,new UTF8Encoding(false));
            Write(new Dictionary<string,object> { {"event","session_start"}, {"schema",1}, {"utc",DateTimeOffset.UtcNow.ToString("o")}, {"local_offset",DateTimeOffset.Now.Offset.ToString()}, {"device",device}, {"os",Environment.OSVersion.ToString()}, {"process_bits",IntPtr.Size*8}, {"scope","receive_only"} });
            Flush();
        }
        public void Write(Dictionary<string,object> e)
        {
            writer.WriteLine(json.Serialize(e));
            if(e.ContainsKey("stage")) { string key=Convert.ToString(e["stage"]); if(!counts.ContainsKey(key)) counts[key]=0; if(Convert.ToString(e["event"])=="midi") { counts[key]++; events++; } }
            if(Convert.ToString(e["event"])=="capture_loss") losses+=Convert.ToInt64(e["count"]);
        }
        public void Flush() { writer.Flush(); }
        public void Dispose()
        {
            Write(new Dictionary<string,object> { {"event","session_end"}, {"utc",DateTimeOffset.UtcNow.ToString("o")}, {"midi_events",events}, {"capture_losses",losses} });
            writer.Dispose();
            var summary=new StringBuilder("Mini32 - resumo da captura\r\n\r\nLog: "+Path+"\r\nMensagens MIDI: "+events+"\r\nPerdas na captura: "+losses+"\r\n\r\nMensagens por etapa:\r\n");
            foreach(var item in counts) summary.AppendLine(item.Key+": "+item.Value);
            summary.AppendLine("\r\nContagens nao comprovam mapeamento correto. Etapas sem mensagem sao inconclusivas; consulte marcadores e notas no JSONL. SysEx e registrado em blocos.");
            File.WriteAllText(summaryPath,summary.ToString(),new UTF8Encoding(false));
        }
    }

    public sealed class Step
    {
        public string Id, Instruction;
        public Step(string id,string text) { Id=id; Instruction=text; }
    }

    public sealed class MonitorWindow : Form
    {
        private readonly string root;
        private readonly ComboBox devices = new ComboBox();
        private readonly Button connect = new Button(), disconnect = new Button(), next = new Button(), back = new Button();
        private readonly Label state = new Label(), guide = new Label(), counter = new Label(), path = new Label();
        private readonly TextBox note = new TextBox();
        private readonly ListBox messages = new ListBox();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly List<Step> steps = new List<Step>();
        private MidiInput input;
        private SessionLog log;
        private int step=-1, tick, midiCount;
        private string deviceList="";
        private string previousLog;
        private bool savingFailed, inTick, smoke;
        public MonitorWindow(string rootDirectory)
        {
            root=rootDirectory;
            Text="Mini32 Stream Deck - Diagnostico MIDI";
            Size=new Size(1020,760); MinimumSize=new Size(820,620); StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Segoe UI",10);
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(16), RowCount=9, ColumnCount=1 };
            for(int i=0;i<9;i++) layout.RowStyles.Add(new RowStyle(i==6?SizeType.Percent:SizeType.Absolute,i==6?100:new int[]{36,44,32,110,42,46,0,46,44}[i]));
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text="Somente leitura MIDI. Sem som, atalhos ou comandos para OBS. Feche programas musicais antes de conectar.",Dock=DockStyle.Fill,AutoEllipsis=true },0,0);
            var bar=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
            devices.Width=310; devices.DropDownStyle=ComboBoxStyle.DropDownList;
            bar.Controls.Add(devices);
            AddButton(bar,"Atualizar",delegate { RefreshDevices(); });
            connect.Text="Conectar e gravar"; connect.AutoSize=true; connect.Click+=delegate { StartCapture(); };bar.Controls.Add(connect);
            disconnect.Text="Encerrar captura";disconnect.AutoSize=true;disconnect.Enabled=false;disconnect.Click+=delegate { StopCapture(); };bar.Controls.Add(disconnect);
            layout.Controls.Add(bar,0,1);
            state.Text="Selecione a entrada MIDI e conecte.";state.Dock=DockStyle.Fill;layout.Controls.Add(state,0,2);
            guide.Text="O roteiro sera iniciado depois da conexao. Cada etapa fica registrada no log. Avance somente depois de soltar as teclas.";
            guide.Dock=DockStyle.Fill;guide.Padding=new Padding(8);guide.BackColor=SystemColors.Info;guide.ForeColor=SystemColors.InfoText;layout.Controls.Add(guide,0,3);
            var navigation=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
            back.Text="Etapa anterior";back.AutoSize=true;back.Enabled=false;back.Click+=delegate { ChangeStep(step-1); };navigation.Controls.Add(back);
            next.Text="Proxima etapa";next.AutoSize=true;next.Enabled=false;next.Click+=delegate { ChangeStep(step+1); };navigation.Controls.Add(next);
            AddButton(navigation,"Marcar: fiz, sem resposta",delegate { Mark("observation","Usuario realizou a etapa e nao observou resposta."); });
            layout.Controls.Add(navigation,0,4);
            var notes=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
            note.Width=470;notes.Controls.Add(note);AddButton(notes,"Registrar observacao",delegate { if(!String.IsNullOrWhiteSpace(note.Text)){Mark("observation",note.Text);note.Clear();} });
            layout.Controls.Add(notes,0,5);
            messages.Dock=DockStyle.Fill;messages.Font=new Font("Consolas",9);messages.HorizontalScrollbar=true;layout.Controls.Add(messages,0,6);
            var bottom=new FlowLayoutPanel {Dock=DockStyle.Fill};
            AddButton(bottom,"Abrir pasta dos logs",delegate { string p=System.IO.Path.Combine(root,"logs");Directory.CreateDirectory(p);Process.Start(new ProcessStartInfo(p){UseShellExecute=true}); });
            counter.AutoSize=true;counter.Text="0 mensagens";bottom.Controls.Add(counter);layout.Controls.Add(bottom,0,7);
            path.Dock=DockStyle.Fill;path.AutoEllipsis=true;layout.Controls.Add(path,0,8);
            BuildSteps();RefreshDevices();
            timer.Interval=80;timer.Tick+=OnTick;timer.Start();
            Application.ThreadException+=OnUiException;
            FormClosing+=delegate(object sender,FormClosingEventArgs e) { timer.Stop();StopCapture();if(input!=null)e.Cancel=true; };
            FormClosed+=delegate { timer.Dispose();Application.ThreadException-=OnUiException; };
        }
        private static void AddButton(Control parent,string text,EventHandler click) { var b=new Button {Text=text,AutoSize=true};b.Click+=click;parent.Controls.Add(b); }
        private void BuildSteps()
        {
            steps.Add(new Step("baseline","Preparacao: nao altere a oitava ainda. Registre na observacao o estado dos LEDs de oitava, se houver. Aguarde alguns segundos sem tocar; depois avance."));
            for(int i=1;i<=32;i++) steps.Add(new Step("key_"+i.ToString("00"),"Tecla fisica "+i+" de 32: conte TODAS as teclas, brancas e pretas, da esquerda para a direita. Pressione uma vez, segure um instante e solte. Avance somente apos soltar."));
            steps.Add(new Step("chord","Pressione e segure as teclas fisicas 1, 2 e 3. Solte cada uma separadamente. Depois avance."));
            steps.Add(new Step("pitch_center","Pitch: deixe no centro/repouso. Mova um pouco e volte ao centro, para que o aparelho envie o valor. Depois avance."));
            steps.Add(new Step("pitch_low","Pitch: mova ate o extremo inferior, segure brevemente e volte ao centro."));
            steps.Add(new Step("pitch_high","Pitch: mova ate o extremo superior, segure brevemente e volte ao centro."));
            steps.Add(new Step("modulation","Modulacao: percorra lentamente todo o alcance e retorne ao inicio."));
            steps.Add(new Step("octave_up_button","Oitava +: pressione e solte SOMENTE o botao uma vez. Nao toque nas teclas nesta etapa. Se nao aparecer mensagem, marque 'fiz, sem resposta'."));
            steps.Add(new Step("octave_up_keys","Apos Oitava +: pressione e solte as teclas fisicas 1, 13 e 25, nessa ordem, uma por vez."));
            steps.Add(new Step("octave_restore_button","Oitava -: pressione e solte SOMENTE o botao uma vez para tentar voltar ao estado inicial. Marque se nao houver resposta."));
            steps.Add(new Step("octave_restore_keys","Confirme o retorno: pressione e solte as teclas fisicas 1, 13 e 25, nessa ordem."));
            steps.Add(new Step("octave_down_button","Oitava -: pressione e solte SOMENTE o botao mais uma vez. Marque se nao houver resposta."));
            steps.Add(new Step("octave_down_keys","Apos Oitava -: pressione e solte as teclas fisicas 1, 13 e 25, nessa ordem."));
            steps.Add(new Step("octave_final_restore","Pressione Oitava + uma vez. Depois pressione e solte a tecla fisica 1 para conferir o retorno. Limites do aparelho podem impedir a transposicao; registre os LEDs."));
            steps.Add(new Step("other_controls","Outros controles: escreva o nome do controle e clique Registrar observacao ANTES de opera-lo. Teste um por vez, sem combinacoes desconhecidas de reset/programacao."));
            steps.Add(new Step("usb_disconnect","Reconexao: desconecte SOMENTE o cabo USB da Mini 32. Aguarde 3 segundos. O log registra mudancas na lista de dispositivos. Depois avance."));
            steps.Add(new Step("usb_reconnect","Reconecte o USB e aguarde 3 segundos. Clique Encerrar captura, Atualizar e Conectar e gravar: abriremos outro log na etapa de verificacao. Nao e necessario repetir as 32 teclas."));
            steps.Add(new Step("reconnection_verify","Verificacao de reconexao: pressione e solte a tecla fisica 1, depois mova o pitch. Confira se surgem mensagens. Registre qualquer dificuldade e encerre a captura."));
            steps.Add(new Step("free","Roteiro concluido. Pode registrar observacoes adicionais e testar livremente. Clique Encerrar captura para finalizar o resumo."));
        }
        private void RefreshDevices()
        {
            if(input!=null) { state.Text="Encerre a captura antes de selecionar outra entrada.";return; }
            try {
                devices.Items.Clear();foreach(Device d in MidiInput.Devices()) devices.Items.Add(d);
                if(devices.Items.Count>0) devices.SelectedIndex=0;
                connect.Enabled=devices.Items.Count>0;
                state.Text=devices.Items.Count==0?"Nenhuma entrada MIDI encontrada. Confira o USB e clique Atualizar.":"Entrada MIDI disponivel. Selecione e clique Conectar e gravar.";
            } catch(Exception ex) { ShowError(ex); }
        }
        private void StartCapture()
        {
            if(input!=null || devices.SelectedItem==null) return;
            try {
                var d=(Device)devices.SelectedItem;
                log=new SessionLog(root,d,"mini32");input=new MidiInput();
                input.Open(d);savingFailed=false;midiCount=0;
                if(previousLog!=null) input.Mark("previous_session",previousLog);
                previousLog=log.Path;
                path.Text=log.Path;
                devices.Enabled=false;connect.Enabled=false;disconnect.Enabled=true;
                state.Text="Capturando: "+d.Name+". Log salvo localmente.";
                deviceList="";
                ChangeStep(step>=0 && steps[step].Id=="usb_reconnect" ? step+1 : 0);
                Pump();
                timer.Start();
            } catch(Exception ex) { StopCapture();ShowError(ex); }
        }
        private void ChangeStep(int index)
        {
            if(input==null || index<0 || index>=steps.Count) return;
            step=index;Step s=steps[step];input.SetStage(s.Id,s.Instruction);
            guide.Text="Etapa "+(step+1)+"/"+steps.Count+" - "+s.Instruction;
            back.Enabled=step>0;next.Enabled=step<steps.Count-1;Pump();
        }
        private void Mark(string type,string text) { if(input==null){state.Text="Conecte para registrar uma observacao.";return;}input.Mark(type,text);Pump(); }
        private void Pump()
        {
            if(input==null || log==null) return;
            foreach(var e in input.Drain()) {
                log.Write(e);
                string type=Convert.ToString(e["event"]);
                if(type=="midi" || type=="midi_error") midiCount++;
                string details=e.ContainsKey("kind")?Convert.ToString(e["kind"])+"  "+Convert.ToString(e["raw_hex"]):e.ContainsKey("text")?Convert.ToString(e["text"]):e.ContainsKey("instruction")?Convert.ToString(e["instruction"]):type;
                if(e.ContainsKey("channel")) details+="  ch="+e["channel"];
                if(e.ContainsKey("note")) details+="  note="+e["note"];
                if(e.ContainsKey("controller")) details+="  CC="+e["controller"];
                if(e.ContainsKey("value")) details+="  value="+e["value"];
                if(e.ContainsKey("driver_overload") && (bool)e["driver_overload"]) details+="  AVISO: atraso no driver";
                messages.Items.Add(Convert.ToString(e["utc"]).Substring(11,12)+" ["+e["stage"]+"] "+details);
                while(messages.Items.Count>300) messages.Items.RemoveAt(0);
            }
            if(messages.Items.Count>0) messages.TopIndex=messages.Items.Count-1;
            log.Flush();counter.Text=midiCount+" mensagens nesta conexao";
        }
        private void OnTick(object sender,EventArgs args)
        {
            if(inTick) return;inTick=true;
            try {
                if(smoke && ++tick>12) {
                    string folder=System.IO.Path.Combine(root,"logs");Directory.CreateDirectory(folder);
                    using(var bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(System.IO.Path.Combine(folder,"ui-smoke.png")); }
                    Close();return;
                }
                Pump();
                if(input!=null && ++tick%25==0) {
                    Device[] list=MidiInput.Devices();var names=new List<string>();bool found=false;
                    foreach(Device d in list) { names.Add(d.Index+":"+d.Signature);if(d.Signature==input.Device.Signature) found=true; }
                    string current=String.Join(";",names.ToArray());
                    if(current!=deviceList){input.Mark("device_snapshot",current);deviceList=current;}
                    if(!found) state.Text="Entrada ausente na lista do Windows. Reconecte; encerre e reabra a captura para recuperar.";
                    else state.Text="Captura aberta. Lista Windows presente; a recepcao so e confirmada quando chegam mensagens.";
                }
            } catch(Exception ex) {
                savingFailed=true;timer.Stop();StopCapture();ShowError(new IOException("Captura interrompida: "+ex.Message,ex));
            } finally { inTick=false; }
        }
        private void StopCapture()
        {
            if(input!=null) {
                try { input.Mark("connection","Encerrando captura");input.Dispose(); }
                catch(Exception ex) {
                    state.Text="Falha ao liberar entrada: "+ex.Message;
                    // Retain the callback and buffers until the native handle is actually closed.
                    connect.Enabled=false;disconnect.Enabled=true;return;
                }
                try { Pump(); }catch(Exception ex){savingFailed=true;state.Text="Erro ao salvar log: "+ex.Message;}
                input=null;
            }
            if(log!=null) { try {log.Dispose();}catch(Exception ex){savingFailed=true;state.Text="Erro ao finalizar log: "+ex.Message;}log=null; }
            devices.Enabled=true;connect.Enabled=devices.Items.Count>0;disconnect.Enabled=false;back.Enabled=false;next.Enabled=false;
            if(!savingFailed) state.Text="Captura encerrada. Log e resumo disponiveis na pasta logs.";
        }
        private void OnUiException(object sender,ThreadExceptionEventArgs e)
        {
            savingFailed=true;timer.Stop();StopCapture();ShowError(e.Exception);
        }
        private void ShowError(Exception ex) { MessageBox.Show(this,ex.Message,"Diagnostico MIDI",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        public void EnableSmokeClose() { smoke=true; }
    }

    public static class Checks
    {
        private static void Assert(bool ok,string name) { if(!ok) throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name); }
        public static void Run()
        {
            var on=Decode.Short(0x643C90);Assert((string)on["kind"]=="NoteOn" && (int)on["note"]==60 && (int)on["velocity"]==100,"Note On");
            var off=Decode.Short(0x003C90);Assert((string)off["kind"]=="NoteOff" && (bool)off["zero_velocity_note_on"],"Note On zero is release");
            Assert((string)Decode.Short(0x403C8F)["kind"]=="NoteOff" && (int)Decode.Short(0x403C8F)["channel"]==16,"Note Off / channel 16");
            Assert((int)Decode.Short(0x0000E0)["value"]==0 && (int)Decode.Short(0x4000E0)["value"]==8192 && (int)Decode.Short(0x7F7FE0)["value"]==16383,"Pitch endpoints and center");
            Assert((int)Decode.Short(0x7F01B0)["controller"]==1 && (int)Decode.Short(0x7F01B0)["value"]==127,"Modulation CC");
            Assert((string)Decode.Short(0x0005C0)["raw_hex"]=="C0 05","Program Change length");
            Assert((string)Decode.Short(0xF8)["raw_hex"]=="F8","Real-time byte length");
            using(var input=new MidiInput()) {
                input.SetStage("key_01","test");input.Mark("observation","physical key 1");input.SetStage("pitch","test");
                var rows=input.Drain();Assert(rows.Count==3 && (string)rows[1]["stage"]=="key_01" && (string)rows[2]["stage"]=="pitch","Ordered stage markers");
                for(int i=0;i<20003;i++) input.Mark("test","overflow");
                var overflow=input.Drain();
                Assert(overflow.Count==20001 && (string)overflow[20000]["event"]=="capture_loss" && (int)overflow[20000]["count"]==3,"Queue overflow is explicitly recorded without losing its count");
            }
        }
        public static void Smoke(string root)
        {
            Device[] devices=MidiInput.Devices();
            if(devices.Length!=1) throw new Exception("Smoke test requires exactly one MIDI input; found "+devices.Length);
            using(var log=new SessionLog(root,devices[0],"smoke"))
            using(var input=new MidiInput()) {
                input.Open(devices[0]);input.SetStage("smoke_no_mapping","Automatic open/close test. No physical mapping inferred.");
                for(int i=0;i<10;i++){Thread.Sleep(100);foreach(var e in input.Drain())log.Write(e);}
                input.Dispose();foreach(var e in input.Drain())log.Write(e);log.Flush();Console.WriteLine("PASS: opened and closed "+devices[0].Name+"; "+log.Path);
            }
        }
    }
}
