import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';

import 'audio_meter.dart';
import 'bridge.dart';
import 'font_preview.dart';

const orange = Color(0xffff914d);
const surface = Color(0xbb2a2524);
const background = Color(0xff1b1919);
const muted = Color(0xffc2b8b2);
const glassStroke = Color(0x4dffffff);

class GlassCard extends StatelessWidget {
  final Widget child;
  const GlassCard({super.key, required this.child});
  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(18),
    child: BackdropFilter(
      filter: ui.ImageFilter.blur(sigmaX: 10, sigmaY: 10),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: surface,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: glassStroke),
        ),
        child: Material(color: Colors.transparent, child: child),
      ),
    ),
  );
}

const kinds = <String, String>{
  'audio': 'Tocar áudio / música',
  'scene': 'OBS · Trocar cena',
  'mute': 'OBS · Silenciar / reativar',
  'preview': 'OBS · Cena da prévia',
  'transition': 'OBS · Fazer transição',
  'recordStart': 'OBS · Iniciar gravação',
  'recordStop': 'OBS · Parar gravação',
  'streamStart': 'OBS · Iniciar transmissão',
  'streamStop': 'OBS · Encerrar transmissão',
  'bank': 'Trocar banco',
  'stop': 'Parar todos os sons',
};
String formatDuration(num seconds) {
  final total = seconds.round();
  final hours = total ~/ 3600;
  final minutes = (total % 3600) ~/ 60;
  final remainder = total % 60;
  if (hours > 0)
    return '$hours:${minutes.toString().padLeft(2, '0')}:${remainder.toString().padLeft(2, '0')}';
  return '$minutes:${remainder.toString().padLeft(2, '0')}';
}

Future<void> main(List<String> args) async {
  WidgetsFlutterBinding.ensureInitialized();
  String fontFamily = 'Roboto';
  final bundledFonts =
      '${File(Platform.resolvedExecutable).parent.path}${Platform.pathSeparator}fonts${Platform.pathSeparator}tt-interphases-pro';
  try {
    if (await loadIncludedInterphases(bundledFonts) ||
        await loadLicensedInterphases(bundledFonts)) {
      fontFamily = interphasesLicensedFamily;
    }
  } catch (e) {
    stderr.writeln('Não foi possível carregar TT Interphases Pro: $e');
  }
  final previewArg = args
      .where((x) => x.startsWith('--font-preview='))
      .firstOrNull;
  if (fontFamily == 'Roboto' && previewArg != null) {
    try {
      if (await loadInterphasesPreview(
        previewArg.substring('--font-preview='.length),
      )) {
        fontFamily = interphasesPreviewFamily;
      }
    } catch (e) {
      stderr.writeln('Não foi possível carregar a prévia local da fonte: $e');
    }
  }
  if (args.contains('--smoke-test')) {
    final root = Directory.systemTemp.createTempSync('Mini32-flutter-smoke-');
    final bridge = EngineBridge(testDataRoot: root.path);
    runApp(Mini32App(bridge: bridge, launch: false, fontFamily: fontFamily));
    unawaited(smokeTest(bridge, args, fontFamily));
    return;
  }
  runApp(Mini32App(bridge: EngineBridge(), fontFamily: fontFamily));
}

Future<void> smokeTest(
  EngineBridge bridge,
  List<String> args,
  String fontFamily,
) async {
  final reportArg = args
      .where((x) => x.startsWith('--smoke-report='))
      .firstOrNull;
  final report = File(
    reportArg?.substring('--smoke-report='.length) ??
        '${bridge.testDataRoot}/report.json',
  );
  try {
    await bridge.start();
    final deadline = DateTime.now().add(const Duration(seconds: 20));
    while (bridge.active == null &&
        bridge.error == null &&
        DateTime.now().isBefore(deadline)) {
      await Future<void>.delayed(const Duration(milliseconds: 100));
    }
    if (!bridge.connected || bridge.active == null || bridge.error != null)
      throw StateError(bridge.error ?? 'Motor não respondeu.');
    await bridge.command('hello');
    await bridge.command('freeKey', {'index': 16, 'replace': 15});
    if (!bridge.keyAvailable(16) ||
        bridge.keyAvailable(15) ||
        (bridge.active?['Keys'] as List).length != 32)
      throw StateError('Configuração não refletida na interface.');
    await report.writeAsString(
      jsonEncode({
        'ok': true,
        'version': '0.3.0-dev.2',
        'fontFamily': fontFamily,
        'dataRoot': bridge.testDataRoot,
        'checks': [
          'native startup',
          'engine subprocess',
          'JSON protocol',
          '32 physical keys / 16 selectable Free positions',
          'free position change roundtrip',
        ],
      }),
    );
  } catch (e) {
    await report.writeAsString(jsonEncode({'ok': false, 'error': '$e'}));
  } finally {
    await bridge.close();
  }
}

class Mini32App extends StatelessWidget {
  final EngineBridge bridge;
  final bool launch;
  final String fontFamily;
  const Mini32App({
    super.key,
    required this.bridge,
    this.launch = true,
    this.fontFamily = 'Roboto',
  });
  @override
  Widget build(BuildContext context) => MaterialApp(
    debugShowCheckedModeBanner: false,
    title: 'Mini32 Stream Deck',
    theme: ThemeData(
      brightness: Brightness.dark,
      useMaterial3: true,
      fontFamily: fontFamily,
      scaffoldBackgroundColor: background,
      colorScheme: ColorScheme.fromSeed(
        seedColor: orange,
        brightness: Brightness.dark,
        primary: orange,
        surface: surface,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: const Color(0x99211e1d),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
        contentPadding: const EdgeInsets.all(13),
      ),
      cardTheme: CardThemeData(
        color: surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(18),
          side: const BorderSide(color: glassStroke),
        ),
      ),
      tooltipTheme: const TooltipThemeData(
        waitDuration: Duration(milliseconds: 400),
      ),
    ),
    home: Studio(bridge: bridge, launch: launch),
  );
}

class Studio extends StatefulWidget {
  final EngineBridge bridge;
  final bool launch;
  const Studio({super.key, required this.bridge, required this.launch});
  @override
  State<Studio> createState() => _StudioState();
}

class _StudioState extends State<Studio> {
  EngineBridge get b => widget.bridge;
  bool dirty = false, importing = false;
  bool maximized = false;
  String search = '';
  String? _shownError;
  @override
  void initState() {
    super.initState();
    b.addListener(update);
    b.onPhysicalSelect = (index) {
      if (!dirty) setState(() => b.selected = index);
    };
    if (widget.launch) unawaited(b.start());
    if (widget.launch) {
      unawaited(
        Future<void>.delayed(
          const Duration(milliseconds: 250),
          () => windowAction('isMaximized'),
        ),
      );
    }
  }

  @override
  void dispose() {
    b.onPhysicalSelect = null;
    b.removeListener(update);
    super.dispose();
  }

  void update() {
    if (!mounted) return;
    if (b.connected && !b.keyAvailable(b.selected))
      b.selected = b.freeKeys.first;
    setState(() {});
    if (b.error != null && b.error != _shownError) {
      _shownError = b.error;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted)
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(b.error!),
              duration: const Duration(seconds: 8),
            ),
          );
      });
    }
  }

  Future<bool> leaveEditor() async {
    if (!dirty) return true;
    final discard = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Há alterações sem salvar'),
        content: const Text(
          'Volte e clique em Salvar tecla para guardar sua configuração.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(c, false),
            child: const Text('Voltar'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(c, true),
            child: const Text('Descartar alterações'),
          ),
        ],
      ),
    );
    if (discard == true) dirty = false;
    return discard == true;
  }

  Future<void> select(int index) async {
    if (index == b.selected) return;
    if (!await leaveEditor()) return;
    if (!b.keyAvailable(index)) {
      if (b.status['useMode'] == true) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Esta posição não está entre as 16 teclas ativas da edição Free.',
            ),
          ),
        );
        return;
      }
      final available = b.freeKeys;
      final replace = await showDialog<int>(
        context: context,
        builder: (c) => AlertDialog(
          title: Text('Ativar tecla ${index + 1}?'),
          content: SizedBox(
            width: 380,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'A edição Free permite escolher 16 das 32 teclas. Selecione uma posição ativa para substituir. A configuração dela será preservada.',
                ),
                const SizedBox(height: 16),
                ConstrainedBox(
                  constraints: const BoxConstraints(maxHeight: 270),
                  child: ListView(
                    shrinkWrap: true,
                    children: available
                        .map(
                          (i) => ListTile(
                            title: Text(
                              'Tecla ${(i + 1).toString().padLeft(2, '0')}',
                            ),
                            subtitle: Text(
                              b.actionAt(i)?['Name'] ?? 'Sem ação',
                            ),
                            onTap: () => Navigator.pop(c, i),
                          ),
                        )
                        .toList(),
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(c),
              child: const Text('Cancelar'),
            ),
          ],
        ),
      );
      if (replace == null || !mounted) return;
      try {
        await b.command('freeKey', {'index': index, 'replace': replace});
      } catch (e) {
        b.fail('$e');
        return;
      }
    }
    if (mounted) setState(() => b.selected = index);
  }

  Future<void> bank(String id) async {
    if (await leaveEditor()) {
      await b.send('bank', {'bank': id});
      if (mounted) setState(() => b.selected = 0);
    }
  }

  Future<void> namedBank(bool rename) async {
    if (!rename && b.isFree) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Bancos adicionais estarão disponíveis na edição Pro.'),
        ),
      );
      return;
    }
    final control = TextEditingController(
      text: rename ? (b.active?['Name'] ?? '') : 'Novo banco',
    );
    final name = await showDialog<String>(
      context: context,
      builder: (c) => AlertDialog(
        title: Text(rename ? 'Nome do banco' : 'Criar banco'),
        content: TextField(
          controller: control,
          autofocus: true,
          maxLength: 60,
          decoration: const InputDecoration(labelText: 'Nome'),
          onSubmitted: (v) => Navigator.pop(c, v),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(c),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(c, control.text),
            child: const Text('Salvar'),
          ),
        ],
      ),
    );
    if (name != null && name.trim().isNotEmpty && await leaveEditor())
      await b.send(rename ? 'renameBank' : 'newBank', {'name': name.trim()});
  }

  Future<void> import() async {
    setState(() => importing = true);
    await b.send('import');
    if (mounted) setState(() => importing = false);
  }

  Future<void> clearSelected() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: Text('Limpar tecla ${b.selected + 1}?'),
        content: const Text(
          'A ação, a imagem e o volume desta tecla serão removidos.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(c, false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(c, true),
            child: const Text('Limpar tecla'),
          ),
        ],
      ),
    );
    if (confirmed == true) await b.send('clear', {'index': b.selected});
  }

  void showHelp() => showDialog<void>(
    context: context,
    builder: (c) => AlertDialog(
      title: const Text('Seu primeiro banco'),
      content: const Text(
        '1. Abra Conexões e conecte a Mini 32.\n2. Importe um áudio.\n3. Clique em uma tecla, configure e salve.\n4. Selecione Usar para controlar a live.\n\nFechar a janela mantém o app na bandeja. Sair encerra os sons.\n\nNo OBS, capture o áudio de Mini32.Engine.exe.',
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(c),
          child: const Text('Entendi'),
        ),
      ],
    ),
  );

  void showConnections() => showDialog<void>(
    context: context,
    builder: (_) => Connections(bridge: b),
  );

  Future<void> runMenu(String value) async {
    switch (value) {
      case 'import':
        await import();
      case 'data':
        await b.send('openData');
      case 'exit':
        await b.close();
      case 'newBank':
        await namedBank(false);
      case 'renameBank':
        await namedBank(true);
      case 'edit':
        if (await leaveEditor()) await b.send('mode', {'use': false});
      case 'use':
        if (await leaveEditor()) await b.send('mode', {'use': true});
      case 'icon':
        await b.send('icon', {'index': b.selected});
      case 'removeIcon':
        await b.send('removeIcon', {'index': b.selected});
      case 'wallpaper':
        await b.send('wallpaper');
      case 'removeWallpaper':
        await b.send('removeWallpaper');
      case 'clear':
        if (await leaveEditor()) await clearSelected();
      case 'learn':
        await b.send('learn', {'index': b.selected});
      case 'connections':
        showConnections();
      case 'refreshObs':
        await b.send('obsRefresh');
      case 'help':
        showHelp();
    }
  }

  Widget menuButton(String title, List<(String, String)> options) =>
      PopupMenuButton<String>(
        tooltip: title,
        onSelected: (value) => unawaited(runMenu(value)),
        itemBuilder: (_) => options
            .map(
              (entry) =>
                  PopupMenuItem<String>(value: entry.$1, child: Text(entry.$2)),
            )
            .toList(),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 9),
          child: Text(
            title,
            style: const TextStyle(fontSize: 12, color: muted),
          ),
        ),
      );

  Future<void> keyMenu(int index, Offset position) async {
    if (!b.keyAvailable(index)) {
      await select(index);
      return;
    }
    final overlay = Overlay.of(context).context.findRenderObject() as RenderBox;
    final choice = await showMenu<String>(
      context: context,
      position: RelativeRect.fromRect(
        position & const Size(1, 1),
        Offset.zero & overlay.size,
      ),
      items: const [
        PopupMenuItem(value: 'edit', child: Text('Editar esta tecla')),
        PopupMenuItem(value: 'icon', child: Text('Escolher imagem')),
        PopupMenuItem(value: 'removeIcon', child: Text('Remover imagem')),
        PopupMenuItem(value: 'learn', child: Text('Aprender tecla física')),
        PopupMenuDivider(),
        PopupMenuItem(value: 'clear', child: Text('Limpar tecla')),
      ],
    );
    if (choice == null || !mounted) return;
    if (!await leaveEditor()) return;
    setState(() => b.selected = index);
    if (choice == 'edit')
      await b.send('mode', {'use': false});
    else
      await runMenu(choice);
  }

  Future<void> bankMenu(String id, Offset position) async {
    final overlay = Overlay.of(context).context.findRenderObject() as RenderBox;
    final choice = await showMenu<String>(
      context: context,
      position: RelativeRect.fromRect(
        position & const Size(1, 1),
        Offset.zero & overlay.size,
      ),
      items: const [
        PopupMenuItem(value: 'select', child: Text('Abrir banco')),
        PopupMenuItem(value: 'rename', child: Text('Renomear banco')),
        PopupMenuItem(value: 'new', child: Text('Novo banco')),
      ],
    );
    if (choice == null || !mounted) return;
    if (choice == 'new') {
      await namedBank(false);
      return;
    }
    await bank(id);
    if (choice == 'rename' && mounted) await namedBank(true);
  }

  Future<void> assignAudio(Map<String, dynamic> asset) async {
    if (!await leaveEditor()) return;
    final isMusic = ((asset['Seconds'] ?? 0) as num) > 120;
    await b.send('action', {
      'index': b.selected,
      'action': {
        'Name': asset['Name'],
        'Kind': 'audio',
        'AssetId': asset['Id'],
        'Volume': isMusic ? 30 : 75,
        'Pitch': !isMusic,
        'Behavior': isMusic ? 'toggle' : 'trigger',
        'Loop': isMusic,
        'Music': isMusic,
        'FadeIn': isMusic ? 500 : 0,
        'FadeOut': isMusic ? 500 : 80,
      },
    });
  }

  Future<void> audioMenu(Map<String, dynamic> asset, Offset position) async {
    final overlay = Overlay.of(context).context.findRenderObject() as RenderBox;
    final choice = await showMenu<String>(
      context: context,
      position: RelativeRect.fromRect(
        position & const Size(1, 1),
        Offset.zero & overlay.size,
      ),
      items: [
        PopupMenuItem(
          value: 'assign',
          child: Text('Atribuir à tecla ${b.selected + 1}'),
        ),
      ],
    );
    if (choice == 'assign' && mounted) await assignAudio(asset);
  }

  Future<void> windowAction(String method) async {
    try {
      final state = await EngineBridge.window.invokeMethod<bool>(method);
      if (mounted && state != null) setState(() => maximized = state);
    } catch (e) {
      if (widget.launch) b.fail('Não foi possível controlar a janela: $e');
    }
  }

  Widget windowButton(
    String label,
    IconData icon,
    String method, {
    bool close = false,
  }) => Tooltip(
    message: label,
    child: InkWell(
      onTap: () => unawaited(windowAction(method)),
      hoverColor: close ? const Color(0xffb33636) : Colors.white12,
      child: SizedBox(
        width: 44,
        height: 40,
        child: Icon(icon, size: 18, color: muted),
      ),
    ),
  );

  @override
  Widget build(BuildContext context) {
    final use = b.status['useMode'] == true;
    final ready = b.connected && b.active != null;
    return Scaffold(
      appBar: AppBar(
        backgroundColor: const Color(0xf0211e1d),
        titleSpacing: 14,
        title: GestureDetector(
          key: const Key('window-drag-area'),
          behavior: HitTestBehavior.opaque,
          onPanStart: (_) => unawaited(windowAction('drag')),
          onDoubleTap: () => unawaited(windowAction('toggleMaximize')),
          child: SizedBox(
            height: 56,
            child: Row(
              children: [
                Image.asset(
                  'assets/branding/final-logo.png',
                  width: 145,
                  height: 38,
                  fit: BoxFit.contain,
                  semanticLabel: 'Mini32 Stream Deck',
                ),
                const SizedBox(width: 22),
                Expanded(child: AudioMeter(bridge: b)),
              ],
            ),
          ),
        ),
        actions: [
          TextButton.icon(
            onPressed: ready ? showConnections : null,
            icon: const Icon(Icons.cable),
            label: const Text('Conexões'),
          ),
          IconButton(
            tooltip: 'Ajuda',
            onPressed: showHelp,
            icon: const Icon(Icons.help_outline),
          ),
          windowButton('Minimizar', Icons.remove, 'minimize'),
          windowButton(
            maximized ? 'Restaurar' : 'Ampliar',
            maximized ? Icons.filter_none : Icons.crop_square,
            'toggleMaximize',
          ),
          windowButton(
            'Fechar para a bandeja',
            Icons.close,
            'closeWindow',
            close: true,
          ),
        ],
      ),
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [Color(0xff27211f), Color(0xff1b1919), Color(0xff171515)],
            stops: [0, 0.48, 1],
          ),
        ),
        child: Stack(
          fit: StackFit.expand,
          children: [
            if ('${b.config['WallpaperFile'] ?? ''}'.isNotEmpty &&
                b.dataRoot.isNotEmpty)
              Positioned.fill(
                child: IgnorePointer(
                  child: Image.file(
                    File(
                      '${b.dataRoot}${Platform.pathSeparator}wallpapers${Platform.pathSeparator}${b.config['WallpaperFile']}',
                    ),
                    fit: BoxFit.cover,
                    opacity: const AlwaysStoppedAnimation(0.5),
                    cacheWidth: 2560,
                    errorBuilder: (_, _, _) => const SizedBox.shrink(),
                  ),
                ),
              ),
            Positioned(
              right: -190,
              top: 70,
              child: IgnorePointer(
                child: Container(
                  width: 700,
                  height: 600,
                  decoration: const BoxDecoration(
                    gradient: RadialGradient(
                      colors: [Color(0x30ff914d), Color(0x00ff914d)],
                    ),
                  ),
                ),
              ),
            ),
            Positioned(
              left: -210,
              bottom: -220,
              child: IgnorePointer(
                child: Container(
                  width: 720,
                  height: 660,
                  decoration: const BoxDecoration(
                    gradient: RadialGradient(
                      colors: [Color(0x307a5141), Color(0x007a5141)],
                    ),
                  ),
                ),
              ),
            ),
            Column(
              children: [
                Container(
                  width: double.infinity,
                  decoration: const BoxDecoration(
                    color: Color(0x712b2523),
                    border: Border(bottom: BorderSide(color: glassStroke)),
                  ),
                  child: SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: Row(
                      children: [
                        menuButton('Arquivo', const [
                          ('import', 'Importar áudio'),
                          ('data', 'Abrir dados e logs'),
                          ('exit', 'Sair do aplicativo'),
                        ]),
                        menuButton('Editar', const [
                          ('edit', 'Modo Editar'),
                          ('icon', 'Escolher imagem da tecla'),
                          ('removeIcon', 'Remover imagem'),
                          ('learn', 'Aprender tecla física'),
                          ('clear', 'Limpar tecla'),
                        ]),
                        menuButton('Bancos', const [
                          ('newBank', 'Novo banco'),
                          ('renameBank', 'Renomear banco'),
                        ]),
                        menuButton('Exibir', const [
                          ('edit', 'Editar'),
                          ('use', 'Usar'),
                          ('wallpaper', 'Escolher wallpaper'),
                          ('removeWallpaper', 'Remover wallpaper'),
                        ]),
                        menuButton('Conexões', const [
                          ('connections', 'Abrir conexões'),
                          ('refreshObs', 'Atualizar cenas do OBS'),
                        ]),
                        menuButton('Ajuda', const [('help', 'Como começar')]),
                      ],
                    ),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(20, 8, 20, 12),
                  child: Wrap(
                    spacing: 18,
                    runSpacing: 12,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      SegmentedButton<bool>(
                        segments: const [
                          ButtonSegment(
                            value: false,
                            icon: Icon(Icons.tune),
                            label: Text('Editar'),
                          ),
                          ButtonSegment(
                            value: true,
                            icon: Icon(Icons.play_arrow),
                            label: Text('Usar'),
                          ),
                        ],
                        selected: {use},
                        onSelectionChanged: ready
                            ? (values) async {
                                if (await leaveEditor())
                                  await b.send('mode', {'use': values.first});
                              }
                            : null,
                      ),
                      Text(
                        use
                            ? 'Sua Mini 32 está no comando'
                            : 'Selecione uma tecla para configurar',
                        style: const TextStyle(color: muted),
                      ),
                      FilledButton.tonalIcon(
                        onPressed: ready ? () => b.send('stop') : null,
                        icon: const Icon(Icons.stop),
                        label: const Text('Parar tudo'),
                      ),
                      SizedBox(
                        width: 200,
                        child: Row(
                          children: [
                            const Icon(Icons.volume_up, size: 20),
                            Expanded(
                              child: Slider(
                                value: ((b.config['Master'] ?? 70) as num)
                                    .toDouble(),
                                min: 0,
                                max: 100,
                                divisions: 100,
                                label: '${b.config['Master'] ?? 70}%',
                                onChanged: ready
                                    ? (v) => setState(
                                        () => b.config['Master'] = v.round(),
                                      )
                                    : null,
                                onChangeEnd: (v) =>
                                    b.send('master', {'value': v.round()}),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                const Divider(height: 1),
                Expanded(
                  child: !ready
                      ? Center(
                          child: Padding(
                            padding: const EdgeInsets.all(32),
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                if (b.error == null)
                                  const CircularProgressIndicator(),
                                const SizedBox(height: 20),
                                Text(
                                  b.error ?? 'Abrindo seu estúdio…',
                                  textAlign: TextAlign.center,
                                ),
                              ],
                            ),
                          ),
                        )
                      : LayoutBuilder(
                          builder: (context, bounds) {
                            final sidebar = bounds.maxWidth >= 900;
                            return Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                if (sidebar)
                                  SizedBox(width: 180, child: bankList()),
                                Expanded(
                                  child: SingleChildScrollView(
                                    padding: const EdgeInsets.all(20),
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        if (!sidebar)
                                          Padding(
                                            padding: const EdgeInsets.only(
                                              bottom: 16,
                                            ),
                                            child: Row(
                                              children: [
                                                Expanded(
                                                  child: DropdownButtonFormField<String>(
                                                    key: ValueKey(
                                                      b.config['ActiveBank'],
                                                    ),
                                                    initialValue:
                                                        b.config['ActiveBank'],
                                                    decoration:
                                                        const InputDecoration(
                                                          labelText: 'Banco',
                                                        ),
                                                    items: b.banks
                                                        .map(
                                                          (
                                                            x,
                                                          ) => DropdownMenuItem<String>(
                                                            value: x['Id'],
                                                            enabled:
                                                                !b.isFree ||
                                                                x['Id'] ==
                                                                    b.config['FreeBank'],
                                                            child: Text(
                                                              '${x['Name']}${b.isFree && x['Id'] != b.config['FreeBank'] ? ' · Pro' : ''}',
                                                            ),
                                                          ),
                                                        )
                                                        .toList(),
                                                    onChanged: (v) {
                                                      if (v != null) bank(v);
                                                    },
                                                  ),
                                                ),
                                                IconButton(
                                                  tooltip: 'Novo banco',
                                                  onPressed: b.isFree
                                                      ? null
                                                      : () => namedBank(false),
                                                  icon: const Icon(Icons.add),
                                                ),
                                                IconButton(
                                                  tooltip: 'Renomear banco',
                                                  onPressed: () =>
                                                      namedBank(true),
                                                  icon: const Icon(
                                                    Icons.edit_outlined,
                                                  ),
                                                ),
                                              ],
                                            ),
                                          ),
                                        LayoutBuilder(
                                          builder: (_, inner) {
                                            final wide = inner.maxWidth >= 880;
                                            final center = controls(use);
                                            final editor = use
                                                ? livePanel()
                                                : KeyEditor(
                                                    key: ValueKey(
                                                      '${b.config['ActiveBank']}-${b.selected}-${b.actionAt(b.selected)?['Id']}',
                                                    ),
                                                    bridge: b,
                                                    index: b.selected,
                                                    onDirty: (v) => dirty = v,
                                                  );
                                            return wide
                                                ? Row(
                                                    crossAxisAlignment:
                                                        CrossAxisAlignment
                                                            .start,
                                                    children: [
                                                      Expanded(child: center),
                                                      const SizedBox(width: 20),
                                                      SizedBox(
                                                        width: 320,
                                                        child: editor,
                                                      ),
                                                    ],
                                                  )
                                                : Column(
                                                    children: [
                                                      center,
                                                      const SizedBox(
                                                        height: 20,
                                                      ),
                                                      editor,
                                                    ],
                                                  );
                                          },
                                        ),
                                      ],
                                    ),
                                  ),
                                ),
                              ],
                            );
                          },
                        ),
                ),
                Container(
                  width: double.infinity,
                  decoration: const BoxDecoration(
                    color: Color(0xd924211f),
                    border: Border(top: BorderSide(color: glassStroke)),
                  ),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 20,
                    vertical: 10,
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        spacing: 22,
                        runSpacing: 4,
                        children: [
                          statusDot(
                            '${b.status['midi'] ?? 'MIDI desconectado'}',
                            b.status['midiConnected'] == true,
                          ),
                          statusDot(
                            '${b.status['obs'] ?? 'OBS desconectado'}',
                            b.status['obsConnected'] == true,
                          ),
                          Text(
                            '${b.status['playing'] ?? 0} sons · Pitch ${((b.status['pitch'] ?? 0) as num).toStringAsFixed(1)}',
                            style: const TextStyle(color: muted, fontSize: 12),
                          ),
                        ],
                      ),
                      const SizedBox(height: 5),
                      Text(
                        '${b.status['last'] ?? 'Pronto'}',
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 12),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget statusDot(String text, bool online) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(
        Icons.circle,
        size: 7,
        color: online ? const Color(0xff72d8a5) : muted,
      ),
      const SizedBox(width: 7),
      Flexible(
        child: Text(
          text,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(fontSize: 12, color: muted),
        ),
      ),
    ],
  );
  Widget bankList() => ClipRect(
    child: BackdropFilter(
      filter: ui.ImageFilter.blur(sigmaX: 10, sigmaY: 10),
      child: Material(
        color: surface,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Padding(
                padding: EdgeInsets.all(10),
                child: Text(
                  'SEUS BANCOS',
                  style: TextStyle(
                    color: muted,
                    fontSize: 11,
                    letterSpacing: 1.8,
                  ),
                ),
              ),
              Expanded(
                child: ListView(
                  children: b.banks
                      .map(
                        (x) => Padding(
                          padding: const EdgeInsets.only(bottom: 5),
                          child: GestureDetector(
                            onSecondaryTapDown: (details) => unawaited(
                              bankMenu(x['Id'], details.globalPosition),
                            ),
                            child: ListTile(
                              dense: true,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(10),
                              ),
                              selected: x['Id'] == b.config['ActiveBank'],
                              selectedTileColor: orange.withValues(alpha: 0.12),
                              selectedColor: orange,
                              leading: const Icon(
                                Icons.layers_outlined,
                                size: 18,
                              ),
                              title: Text(x['Name']),
                              subtitle:
                                  b.isFree && x['Id'] != b.config['FreeBank']
                                  ? const Text('Pro')
                                  : null,
                              enabled:
                                  !b.isFree || x['Id'] == b.config['FreeBank'],
                              onTap: () => bank(x['Id']),
                            ),
                          ),
                        ),
                      )
                      .toList(),
                ),
              ),
              TextButton.icon(
                onPressed: b.isFree ? null : () => namedBank(false),
                icon: const Icon(Icons.add),
                label: const Text('Novo banco'),
              ),
              TextButton(
                onPressed: () => namedBank(true),
                child: const Text('Renomear banco'),
              ),
            ],
          ),
        ),
      ),
    ),
  );
  Widget controls(bool use) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      const Text(
        'SEU ESPAÇO DE CONTROLE',
        style: TextStyle(color: muted, fontSize: 11, letterSpacing: 1.5),
      ),
      const SizedBox(height: 6),
      Row(
        children: [
          Expanded(
            child: Text(
              b.active?['Name'] ?? '',
              style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w600),
            ),
          ),
          Text(
            '${EngineBridge.list(b.active?['Keys']).asMap().entries.where((e) => b.keyAvailable(e.key) && e.value['ActionId'] != '').length} / ${b.isFree ? 16 : 32} teclas ativas',
            style: const TextStyle(color: muted),
          ),
        ],
      ),
      const SizedBox(height: 20),
      Piano(
        bridge: b,
        selected: b.selected,
        use: use,
        onSelect: select,
        onContextMenu: (index, position) => unawaited(keyMenu(index, position)),
      ),
      const SizedBox(height: 22),
      LayoutBuilder(
        builder: (_, box) {
          final columns = (box.maxWidth / 132).floor().clamp(2, 8);
          return GridView.builder(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            itemCount: 32,
            gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: columns,
              mainAxisExtent: 72,
              crossAxisSpacing: 8,
              mainAxisSpacing: 8,
            ),
            itemBuilder: (_, i) {
              final action = b.actionAt(i);
              final active = i == b.selected;
              final locked = !b.keyAvailable(i);
              final iconFile = b.active?['Keys'][i]['IconFile'] ?? '';
              return ActionTile(
                bridge: b,
                index: i,
                use: use,
                onSelect: () => select(i),
                onContextMenu: (position) => unawaited(keyMenu(i, position)),
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(13),
                  child: Container(
                    decoration: BoxDecoration(
                      color: active
                          ? const Color(0xbd573a2f)
                          : const Color(0x992a2524),
                      borderRadius: BorderRadius.circular(13),
                      border: Border.all(color: active ? orange : glassStroke),
                    ),
                    child: Stack(
                      children: [
                        if (iconFile.isNotEmpty && b.dataRoot.isNotEmpty)
                          Positioned.fill(
                            child: Image.file(
                              File(
                                '${b.dataRoot}${Platform.pathSeparator}icons${Platform.pathSeparator}$iconFile',
                              ),
                              fit: BoxFit.cover,
                              opacity: const AlwaysStoppedAnimation(0.5),
                              errorBuilder: (_, _, _) =>
                                  const SizedBox.shrink(),
                            ),
                          ),
                        const Positioned.fill(
                          child: DecoratedBox(
                            decoration: BoxDecoration(
                              gradient: LinearGradient(
                                colors: [Color(0xdd1b1919), Color(0x881b1919)],
                              ),
                            ),
                          ),
                        ),
                        Padding(
                          padding: const EdgeInsets.all(10),
                          child: Row(
                            children: [
                              Text(
                                '${i + 1}'.padLeft(2, '0'),
                                style: TextStyle(
                                  color: active ? orange : muted,
                                  fontSize: 12,
                                ),
                              ),
                              const SizedBox(width: 9),
                              Expanded(
                                child: Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      locked
                                          ? 'Escolher posição'
                                          : action?['Name'] ?? 'Configurar',
                                      maxLines: 2,
                                      overflow: TextOverflow.ellipsis,
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: action == null || locked
                                            ? muted
                                            : Colors.white,
                                      ),
                                    ),
                                    if (action != null && !locked)
                                      Text(
                                        action['Kind'] == 'audio'
                                            ? (action['Music'] == true
                                                  ? 'Música'
                                                  : 'Vinheta')
                                            : kinds[action['Kind']] ?? '',
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(
                                          fontSize: 10,
                                          color: muted,
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                        if (locked)
                          const Positioned(
                            right: 8,
                            top: 8,
                            child: Icon(
                              Icons.lock_outline,
                              size: 15,
                              color: muted,
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
              );
            },
          );
        },
      ),
      const SizedBox(height: 24),
      GlassCard(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Biblioteca de áudio',
                      style: TextStyle(fontWeight: FontWeight.w600),
                    ),
                  ),
                  FilledButton.tonalIcon(
                    onPressed: importing ? null : import,
                    icon: const Icon(Icons.add, size: 18),
                    label: Text(importing ? 'Importando…' : 'Importar'),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              TextField(
                decoration: const InputDecoration(
                  prefixIcon: Icon(Icons.search),
                  hintText: 'Buscar uma vinheta',
                ),
                onChanged: (v) => setState(() => search = v),
              ),
              const SizedBox(height: 8),
              if (b.assets.isEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Sua biblioteca está vazia.',
                        style: TextStyle(color: muted),
                      ),
                      TextButton(
                        onPressed: () => b.send('example'),
                        child: const Text('Adicionar som de exemplo'),
                      ),
                    ],
                  ),
                ),
              ...b.assets
                  .where(
                    (a) => '${a['Name']}'.toLowerCase().contains(
                      search.toLowerCase(),
                    ),
                  )
                  .map(
                    (a) => GestureDetector(
                      onSecondaryTapDown: (details) =>
                          unawaited(audioMenu(a, details.globalPosition)),
                      child: ListTile(
                        contentPadding: EdgeInsets.zero,
                        leading: const Icon(Icons.graphic_eq, color: orange),
                        title: Text(
                          a['Name'],
                          style: const TextStyle(fontSize: 13),
                        ),
                        subtitle: Text(formatDuration(a['Seconds'] as num)),
                        trailing: TextButton(
                          onPressed: use ? null : () => assignAudio(a),
                          child: const Text('Atribuir'),
                        ),
                      ),
                    ),
                  ),
            ],
          ),
        ),
      ),
    ],
  );
  Widget livePanel() => GlassCard(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.graphic_eq, color: orange, size: 32),
          const SizedBox(height: 14),
          const Text('Pronto para sua live', style: TextStyle(fontSize: 20)),
          const SizedBox(height: 12),
          const Text(
            'Use a Mini 32 ou clique nas teclas. Para mudar as configurações, volte ao modo Editar.',
            style: TextStyle(color: muted),
          ),
          const SizedBox(height: 24),
          LinearProgressIndicator(
            value: ((b.status['peak'] ?? 0) as num).toDouble().clamp(0, 1),
            minHeight: 6,
          ),
          const SizedBox(height: 12),
          Text('${b.status['playing'] ?? 0} sons em reprodução'),
          const SizedBox(height: 20),
          const Text(
            'Volume da tecla durante a live',
            style: TextStyle(fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 10),
          DropdownButtonFormField<int>(
            key: ValueKey('live-key-${b.selected}'),
            initialValue: b.selected,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Tecla'),
            items: b.isFree
                ? b.freeKeys
                      .map(
                        (i) => DropdownMenuItem(
                          value: i,
                          child: Text(
                            '${(i + 1).toString().padLeft(2, '0')} · ${b.actionAt(i)?['Name'] ?? 'Sem ação'}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList()
                : List.generate(
                    32,
                    (i) => DropdownMenuItem(
                      value: i,
                      child: Text(
                        '${(i + 1).toString().padLeft(2, '0')} · ${b.actionAt(i)?['Name'] ?? 'Sem ação'}',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ),
            onChanged: (i) {
              if (i != null) setState(() => b.selected = i);
            },
          ),
          if (b.actionAt(b.selected)?['Kind'] == 'audio') ...[
            const SizedBox(height: 12),
            Text(
              '${b.active?['Keys'][b.selected]['Volume'] ?? b.actionAt(b.selected)?['Volume'] ?? 75}%',
              style: const TextStyle(color: orange),
            ),
            Slider(
              value:
                  ((b.active?['Keys'][b.selected]['Volume'] ??
                              b.actionAt(b.selected)?['Volume'] ??
                              75)
                          as num)
                      .toDouble(),
              min: 0,
              max: 100,
              divisions: 100,
              onChanged: (value) => setState(
                () => b.active?['Keys'][b.selected]['Volume'] = value.round(),
              ),
              onChangeEnd: (value) => b.send('keyVolume', {
                'index': b.selected,
                'volume': value.round(),
              }),
            ),
          ],
          const SizedBox(height: 20),
          const Text(
            'Pode minimizar: o motor continua recebendo suas teclas.',
            style: TextStyle(color: muted),
          ),
        ],
      ),
    ),
  );
}

class ActionTile extends StatelessWidget {
  final EngineBridge bridge;
  final int index;
  final bool use;
  final VoidCallback onSelect;
  final ValueChanged<Offset>? onContextMenu;
  final Widget child;
  const ActionTile({
    super.key,
    required this.bridge,
    required this.index,
    required this.use,
    required this.onSelect,
    this.onContextMenu,
    required this.child,
  });
  @override
  Widget build(BuildContext context) => Semantics(
    button: true,
    label: 'Tecla ${index + 1}',
    child: FocusableActionDetector(
      actions: {
        ActivateIntent: CallbackAction<ActivateIntent>(
          onInvoke: (_) {
            if (!use) {
              onSelect();
            } else {
              unawaited(
                bridge.send('press', {'index': index, 'token': -100 - index}),
              );
              unawaited(bridge.send('release', {'token': -100 - index}));
            }
            return null;
          },
        ),
      },
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        child: Listener(
          onPointerDown: (event) {
            if (use && (event.buttons & kPrimaryMouseButton) != 0)
              bridge.send('press', {'index': index, 'token': -100 - index});
          },
          onPointerUp: (_) {
            if (use) bridge.send('release', {'token': -100 - index});
          },
          onPointerCancel: (_) {
            if (use) bridge.send('release', {'token': -100 - index});
          },
          child: GestureDetector(
            onTap: use ? null : onSelect,
            onSecondaryTapDown: onContextMenu == null
                ? null
                : (details) => onContextMenu!(details.globalPosition),
            child: child,
          ),
        ),
      ),
    ),
  );
}

class Piano extends StatelessWidget {
  final EngineBridge bridge;
  final int selected;
  final bool use;
  final ValueChanged<int> onSelect;
  final void Function(int, Offset)? onContextMenu;
  const Piano({
    super.key,
    required this.bridge,
    required this.selected,
    required this.use,
    required this.onSelect,
    this.onContextMenu,
  });
  bool black(int index) => {1, 3, 6, 8, 10}.contains((53 + index) % 12);
  @override
  Widget build(BuildContext context) => GlassCard(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        children: [
          const Row(
            children: [
              Text(
                'AMW  MINI 32',
                style: TextStyle(
                  fontSize: 11,
                  letterSpacing: 1.4,
                  color: muted,
                ),
              ),
              Spacer(),
              Text(
                '32 teclas independentes',
                style: TextStyle(fontSize: 11, color: muted),
              ),
            ],
          ),
          const SizedBox(height: 16),
          LayoutBuilder(
            builder: (_, box) {
              final whites = List.generate(
                32,
                (i) => i,
              ).where((i) => !black(i)).toList();
              final w = box.maxWidth / whites.length;
              final children = <Widget>[];
              Widget key(int i, double left, double width, bool dark) =>
                  Positioned(
                    left: left,
                    width: width,
                    top: 0,
                    height: dark ? 88 : 140,
                    child: ActionTile(
                      bridge: bridge,
                      index: i,
                      use: use,
                      onSelect: () => onSelect(i),
                      onContextMenu: onContextMenu == null
                          ? null
                          : (position) => onContextMenu!(i, position),
                      child: Container(
                        margin: const EdgeInsets.symmetric(horizontal: 1),
                        decoration: BoxDecoration(
                          color: selected == i
                              ? orange
                              : dark
                              ? const Color(0xff292625)
                              : const Color(0xffe1e3e2),
                          borderRadius: const BorderRadius.vertical(
                            bottom: Radius.circular(4),
                          ),
                          border: Border.all(color: background),
                        ),
                        alignment: Alignment.bottomCenter,
                        padding: const EdgeInsets.only(bottom: 9),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.end,
                          children: [
                            if (bridge.actionAt(i) != null)
                              Icon(
                                Icons.circle,
                                size: 5,
                                color: selected == i ? background : orange,
                              ),
                            const SizedBox(height: 6),
                            Text(
                              '${i + 1}',
                              style: TextStyle(
                                fontSize: 9,
                                color: selected == i || !dark
                                    ? background
                                    : muted,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
              for (int n = 0; n < whites.length; n++) {
                children.add(key(whites[n], n * w, w, false));
              }
              for (int i = 0; i < 32; i++) {
                if (black(i)) {
                  final before = whites.where((x) => x < i).length;
                  children.add(key(i, before * w - w * 0.30, w * 0.60, true));
                }
              }
              return SizedBox(height: 140, child: Stack(children: children));
            },
          ),
        ],
      ),
    ),
  );
}

class KeyEditor extends StatefulWidget {
  final EngineBridge bridge;
  final int index;
  final ValueChanged<bool> onDirty;
  const KeyEditor({
    super.key,
    required this.bridge,
    required this.index,
    required this.onDirty,
  });
  @override
  State<KeyEditor> createState() => _KeyEditorState();
}

class _KeyEditorState extends State<KeyEditor> {
  late Map<String, dynamic> draft;
  late TextEditingController name, group, start, end;
  late int keyVolume;
  bool saving = false, dirty = false, shared = true;
  EngineBridge get b => widget.bridge;
  @override
  void initState() {
    super.initState();
    draft = {
      'Name': 'Tecla ${widget.index + 1}',
      'Kind': 'audio',
      'AssetId': '',
      'Target': '',
      'Behavior': 'trigger',
      'Volume': 75,
      'Pitch': true,
      'Loop': false,
      'Group': '',
      'Start': 0,
      'End': 0,
      'FadeIn': 0,
      'FadeOut': 80,
      'Music': false,
      'Duck': false,
      ...?b.actionAt(widget.index),
    };
    final binding = b.active?['Keys'][widget.index];
    keyVolume = (binding?['Volume'] ?? draft['Volume'] ?? 75) as int;
    name = TextEditingController(text: draft['Name']);
    group = TextEditingController(text: draft['Group']);
    start = TextEditingController(text: '${draft['Start']}');
    end = TextEditingController(text: '${draft['End']}');
  }

  @override
  void dispose() {
    name.dispose();
    group.dispose();
    start.dispose();
    end.dispose();
    super.dispose();
  }

  void change(String key, dynamic value) {
    setState(() {
      draft[key] = value;
      dirty = true;
    });
    widget.onDirty(true);
  }

  Future<void> save() async {
    setState(() => saving = true);
    try {
      draft['Start'] = double.parse(start.text.replaceAll(',', '.'));
      draft['End'] = double.parse(end.text.replaceAll(',', '.'));
      await b.command('action', {
        'index': widget.index,
        'action': draft,
        'shared': shared,
        'volume': keyVolume,
      });
      widget.onDirty(false);
      if (mounted) setState(() => dirty = false);
    } catch (e) {
      b.fail('Não foi possível salvar: $e');
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  Widget check(String key, String title) => CheckboxListTile(
    dense: true,
    contentPadding: EdgeInsets.zero,
    title: Text(title, style: const TextStyle(fontSize: 13)),
    value: draft[key] == true,
    onChanged: (v) => change(key, v == true),
  );
  Widget dropdown(String key, String label, Map<String, String> values) =>
      Padding(
        padding: const EdgeInsets.only(bottom: 14),
        child: DropdownButtonFormField<String>(
          key: ValueKey('$key-${draft[key]}'),
          initialValue: values.containsKey(draft[key]) ? draft[key] : null,
          isExpanded: true,
          decoration: InputDecoration(labelText: label),
          items: values.entries
              .map(
                (e) => DropdownMenuItem(
                  value: e.key,
                  child: Text(e.value, overflow: TextOverflow.ellipsis),
                ),
              )
              .toList(),
          onChanged: (v) => change(key, v),
        ),
      );
  @override
  Widget build(BuildContext context) {
    final audio = draft['Kind'] == 'audio';
    final actionId = b.actionAt(widget.index)?['Id'];
    final shares = b.banks
        .expand((x) => EngineBridge.list(x['Keys']))
        .where((k) => actionId != null && k['ActionId'] == actionId)
        .length;
    return GlassCard(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'TECLA ${(widget.index + 1).toString().padLeft(2, '0')}',
              style: const TextStyle(
                color: orange,
                fontSize: 12,
                letterSpacing: 1.6,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              dirty ? 'Alterações sem salvar' : 'Configuração da tecla',
              style: const TextStyle(fontSize: 18),
            ),
            const SizedBox(height: 20),
            TextField(
              controller: name,
              maxLength: 80,
              decoration: const InputDecoration(labelText: 'Nome da tecla'),
              onChanged: (v) => change('Name', v),
            ),
            const SizedBox(height: 8),
            dropdown('Kind', 'O que ela faz?', kinds),
            OutlinedButton.icon(
              onPressed: () => b.send('icon', {'index': widget.index}),
              icon: const Icon(Icons.add_photo_alternate_outlined),
              label: const Text('Escolher imagem da tecla'),
            ),
            if ((b.active?['Keys'][widget.index]['IconFile'] ?? '') != '')
              TextButton.icon(
                onPressed: () => b.send('removeIcon', {'index': widget.index}),
                icon: const Icon(Icons.hide_image_outlined),
                label: const Text('Remover imagem'),
              ),
            const SizedBox(height: 12),
            if (audio) ...[
              dropdown('AssetId', 'Áudio da biblioteca', {
                for (final a in b.assets) a['Id']: a['Name'],
              }),
              dropdown('Behavior', 'Ao apertar a tecla', const {
                'trigger': 'Tocar / reiniciar',
                'hold': 'Tocar enquanto segura',
                'toggle': 'Alternar tocar / parar',
              }),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('Volume desta tecla'),
                  Text('$keyVolume%', style: const TextStyle(color: orange)),
                ],
              ),
              Slider(
                value: keyVolume.toDouble(),
                min: 0,
                max: 100,
                divisions: 100,
                onChanged: (v) {
                  setState(() {
                    keyVolume = v.round();
                    dirty = true;
                  });
                  widget.onDirty(true);
                },
              ),
              const Text(
                'Mesmo usando a ação de outra tecla, este volume é independente.',
                style: TextStyle(fontSize: 11, color: muted),
              ),
              const SizedBox(height: 10),
              check('Pitch', 'Permitir controle de pitch'),
              check('Loop', 'Repetir em loop'),
              ExpansionTile(
                tilePadding: EdgeInsets.zero,
                title: const Text(
                  'Ajustes do áudio',
                  style: TextStyle(fontSize: 13),
                ),
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: start,
                          decoration: const InputDecoration(
                            labelText: 'Início (s)',
                          ),
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          onChanged: (v) {
                            change('Start', draft['Start']);
                          },
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: TextField(
                          controller: end,
                          decoration: const InputDecoration(
                            labelText: 'Fim (0 = total)',
                          ),
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          onChanged: (v) {
                            change('End', draft['End']);
                          },
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      const Expanded(child: Text('Fade de entrada')),
                      DropdownButton<int>(
                        value: draft['FadeIn'],
                        items: [0, 80, 250, 500, 1000, 2000, 5000]
                            .map(
                              (n) => DropdownMenuItem(
                                value: n,
                                child: Text('$n ms'),
                              ),
                            )
                            .toList(),
                        onChanged: (v) => change('FadeIn', v),
                      ),
                    ],
                  ),
                  Row(
                    children: [
                      const Expanded(child: Text('Fade ao parar')),
                      DropdownButton<int>(
                        value: draft['FadeOut'],
                        items: [0, 80, 250, 500, 1000, 2000, 5000]
                            .map(
                              (n) => DropdownMenuItem(
                                value: n,
                                child: Text('$n ms'),
                              ),
                            )
                            .toList(),
                        onChanged: (v) => change('FadeOut', v),
                      ),
                    ],
                  ),
                  TextField(
                    controller: group,
                    decoration: const InputDecoration(
                      labelText: 'Grupo exclusivo (opcional)',
                    ),
                    onChanged: (v) => change('Group', v),
                  ),
                  const SizedBox(height: 8),
                  check('Music', 'Este som é música de fundo'),
                  check('Duck', 'Abaixar a música enquanto toca'),
                ],
              ),
            ],
            if (['scene', 'preview'].contains(draft['Kind']))
              dropdown('Target', 'Cena do OBS', {
                for (final s in b.scenes) s: s,
              }),
            if (draft['Kind'] == 'mute')
              dropdown('Target', 'Entrada de áudio do OBS', {
                for (final s in b.inputs) s: s,
              }),
            if (draft['Kind'] == 'bank')
              dropdown('Target', 'Banco de destino', {
                for (final s in b.banks) s['Id']: s['Name'],
              }),
            if ([
              'streamStart',
              'streamStop',
              'recordStop',
            ].contains(draft['Kind']))
              const Padding(
                padding: EdgeInsets.only(bottom: 16),
                child: Text(
                  'Para executar: mantenha a tecla pressionada por 1 segundo.',
                  style: TextStyle(color: orange),
                ),
              ),
            if (shares > 1)
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                value: shared,
                onChanged: (v) => setState(() => shared = v == true),
                title: Text(
                  'Atualizar as $shares teclas que compartilham esta ação',
                  style: const TextStyle(fontSize: 12),
                ),
              ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: saving ? null : save,
              icon: const Icon(Icons.check, size: 18),
              label: Text(saving ? 'Salvando…' : 'Salvar tecla'),
            ),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: () => b.send('learn', {'index': widget.index}),
              icon: const Icon(Icons.piano, size: 18),
              label: const Text('Aprender tecla física'),
            ),
            if (b.actions.isNotEmpty)
              TextButton(
                onPressed: () async {
                  final id = await showDialog<String>(
                    context: context,
                    builder: (c) => SimpleDialog(
                      title: const Text('Compartilhar uma ação'),
                      children: b.actions
                          .map(
                            (a) => SimpleDialogOption(
                              onPressed: () => Navigator.pop(c, a['Id']),
                              child: Text(a['Name']),
                            ),
                          )
                          .toList(),
                    ),
                  );
                  if (id != null) {
                    await b.send('share', {
                      'index': widget.index,
                      'actionId': id,
                    });
                    widget.onDirty(false);
                  }
                },
                child: const Text('Usar ação de outra tecla'),
              ),
            TextButton(
              onPressed: () async {
                try {
                  await b.command('clear', {'index': widget.index});
                  widget.onDirty(false);
                  if (mounted)
                    setState(() {
                      draft = {
                        'Name': 'Tecla ${widget.index + 1}',
                        'Kind': 'audio',
                        'AssetId': '',
                        'Target': '',
                        'Behavior': 'trigger',
                        'Volume': 75,
                        'Pitch': true,
                        'Loop': false,
                        'Group': '',
                        'Start': 0,
                        'End': 0,
                        'FadeIn': 0,
                        'FadeOut': 80,
                        'Music': false,
                        'Duck': false,
                      };
                      name.text = draft['Name'];
                      group.clear();
                      start.text = '0';
                      end.text = '0';
                      keyVolume = 75;
                      dirty = false;
                    });
                } catch (e) {
                  b.fail('$e');
                }
              },
              child: const Text('Limpar tecla'),
            ),
            const SizedBox(height: 8),
            const Text(
              'Salve e vá para Usar para testar. No modo Editar, a controladora seleciona a tecla.',
              style: TextStyle(fontSize: 12, color: muted),
            ),
          ],
        ),
      ),
    );
  }
}

class Connections extends StatefulWidget {
  final EngineBridge bridge;
  const Connections({super.key, required this.bridge});
  @override
  State<Connections> createState() => _ConnectionsState();
}

class _ConnectionsState extends State<Connections> {
  EngineBridge get b => widget.bridge;
  late TextEditingController port, password;
  String? midi, output;
  bool remember = true, busy = false;
  @override
  void initState() {
    super.initState();
    port = TextEditingController(text: '${b.config['ObsPort'] ?? 4455}');
    password = TextEditingController();
    output = b.config['Output'] ?? '';
    midi = b.config['MidiSignature'];
    b.addListener(refresh);
    b.send('devices');
  }

  void refresh() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    b.removeListener(refresh);
    port.dispose();
    password.dispose();
    super.dispose();
  }

  Future<void> connectObs() async {
    setState(() => busy = true);
    try {
      await b.command('obs', {
        'port': int.parse(port.text),
        'password': password.text,
        'remember': remember,
      });
      password.clear();
    } catch (e) {
      b.fail('$e');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final devices = EngineBridge.list(b.devices['midi']);
    final signatures = {for (final d in devices) d['Signature']: d['Name']};
    return AlertDialog(
      title: const Text('Conexões'),
      content: SizedBox(
        width: 520,
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'MINI 32',
                style: TextStyle(
                  color: orange,
                  letterSpacing: 1.5,
                  fontSize: 12,
                ),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: signatures.containsKey(midi) ? midi : null,
                isExpanded: true,
                decoration: const InputDecoration(
                  labelText: 'Controladora MIDI',
                ),
                items: signatures.entries
                    .map(
                      (e) => DropdownMenuItem<String>(
                        value: e.key,
                        child: Text(e.value),
                      ),
                    )
                    .toList(),
                onChanged: (v) => setState(() => midi = v),
              ),
              Wrap(
                spacing: 8,
                children: [
                  TextButton(
                    onPressed: () => b.send('devices'),
                    child: const Text('Atualizar'),
                  ),
                  TextButton(
                    onPressed: midi == null
                        ? null
                        : () => b.send('midi', {
                            'signature': midi,
                            'remember': true,
                          }),
                    child: const Text('Conectar'),
                  ),
                  TextButton(
                    onPressed: () => b.send('midiOff'),
                    child: const Text('Desconectar'),
                  ),
                ],
              ),
              Text(
                '${b.status['midi'] ?? ''}',
                style: const TextStyle(color: muted, fontSize: 12),
              ),
              const SizedBox(height: 20),
              DropdownButtonFormField<String>(
                initialValue:
                    output == '' ||
                        EngineBridge.list(
                          b.devices['outputs'],
                        ).any((x) => x['Name'] == output)
                    ? output
                    : null,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Saída de áudio'),
                items: [
                  const DropdownMenuItem(
                    value: '',
                    child: Text('Padrão do Windows'),
                  ),
                  ...EngineBridge.list(b.devices['outputs'])
                      .map((d) => d['Name'] as String)
                      .toSet()
                      .map(
                        (name) =>
                            DropdownMenuItem(value: name, child: Text(name)),
                      ),
                ],
                onChanged: (v) {
                  setState(() => output = v);
                  b.send('output', {'name': v ?? ''});
                },
              ),
              const SizedBox(height: 8),
              const Text(
                'No OBS, capture o áudio de Mini32.Engine.exe. Evite capturar também o mesmo som pelo áudio do desktop.',
                style: TextStyle(fontSize: 12, color: muted),
              ),
              const Divider(height: 32),
              const Text(
                'OBS STUDIO',
                style: TextStyle(
                  color: orange,
                  letterSpacing: 1.5,
                  fontSize: 12,
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'OBS → Ferramentas → Configurações do servidor WebSocket. Habilite o servidor e copie a senha.',
                style: TextStyle(color: muted, fontSize: 12),
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  SizedBox(
                    width: 100,
                    child: TextField(
                      controller: port,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(labelText: 'Porta'),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: TextField(
                      controller: password,
                      obscureText: true,
                      decoration: const InputDecoration(labelText: 'Senha OBS'),
                    ),
                  ),
                ],
              ),
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                value: remember,
                onChanged: (v) => setState(() => remember = v == true),
                title: const Text(
                  'Lembrar e reconectar automaticamente',
                  style: TextStyle(fontSize: 13),
                ),
              ),
              Wrap(
                spacing: 8,
                children: [
                  FilledButton(
                    onPressed: busy ? null : connectObs,
                    child: Text(busy ? 'Conectando…' : 'Conectar OBS'),
                  ),
                  TextButton(
                    onPressed: () => b.send('obsRefresh'),
                    child: const Text('Atualizar cenas'),
                  ),
                  TextButton(
                    onPressed: () => b.send('obsOff'),
                    child: const Text('Desconectar'),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Text(
                '${b.status['obs'] ?? ''}',
                style: const TextStyle(fontSize: 12, color: muted),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Concluído'),
        ),
      ],
    );
  }
}
