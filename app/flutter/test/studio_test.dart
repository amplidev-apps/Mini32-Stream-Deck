import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mini32_stream_deck/bridge.dart';
import 'package:mini32_stream_deck/audio_meter.dart';
import 'package:mini32_stream_deck/font_preview.dart';
import 'package:mini32_stream_deck/main.dart';

class FakeEngine extends EngineBridge {
  final sent = <String>[];
  final sentArgs = <Map<String, dynamic>>[];
  FakeEngine() {
    connected = true;
    config = {
      'Master': 70,
      'ActiveBank': 'live',
      'FreeBank': 'live',
      'Banks': [
        {
          'Id': 'live',
          'Name': 'Live principal',
          'Keys': List.generate(
            32,
            (i) => {
              'Note': 53 + i,
              'Channel': 1,
              'ActionId': i == 0 ? 'a' : '',
            },
          ),
        },
      ],
      'Actions': [
        {
          'Id': 'a',
          'Name': 'Aplausos',
          'Kind': 'audio',
          'AssetId': 'sound',
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
        },
      ],
      'Assets': [
        {'Id': 'sound', 'Name': 'Aplausos', 'Seconds': 3.0},
      ],
    };
    status = {'useMode': false, 'last': 'Pronto', 'playing': 0};
    devices = {'midi': [], 'outputs': []};
  }
  @override
  Future<dynamic> command(
    String op, [
    Map<String, dynamic> args = const {},
  ]) async {
    sent.add(op);
    sentArgs.add(args);
    if (op == 'mode') status['useMode'] = args['use'];
    if (op == 'freeKey') {
      final keys = (config['FreeKeys'] as List?) ?? List.generate(16, (i) => i);
      keys.remove(args['replace']);
      keys.add(args['index']);
      config['FreeKeys'] = keys;
    }
    notifyListeners();
    return null;
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    final fonts = <String, String>{
      'Roboto': 'assets/fonts/roboto-regular.ttf',
      'MaterialIcons':
          '../../.tools/flutter/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
    };
    for (final entry in fonts.entries) {
      final loader = FontLoader(entry.key);
      loader.addFont(
        File(
          entry.value,
        ).readAsBytes().then((bytes) => ByteData.sublistView(bytes)),
      );
      await loader.load();
    }
  });
  for (final size in [
    const Size(1280, 900),
    const Size(900, 700),
    const Size(760, 640),
  ]) {
    testWidgets('Studio fits $size and switches use mode', (tester) async {
      tester.view.physicalSize = size;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final engine = FakeEngine();
      final capture = GlobalKey();
      await tester.pumpWidget(
        RepaintBoundary(
          key: capture,
          child: Mini32App(bridge: engine, launch: false),
        ),
      );
      await tester.runAsync(
        () => precacheImage(
          const AssetImage('assets/branding/final-logo.png'),
          tester.element(find.byType(Image)),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.runAsync(() async {
        final boundary =
            capture.currentContext!.findRenderObject()!
                as RenderRepaintBoundary;
        final image = await boundary.toImage();
        final png = await image.toByteData(format: ui.ImageByteFormat.png);
        await Directory('build/test-screenshots').create(recursive: true);
        await File(
          'build/test-screenshots/studio-${size.width.toInt()}.png',
        ).writeAsBytes(png!.buffer.asUint8List());
        image.dispose();
      });
      expect(find.byType(Image), findsOneWidget);
      expect(find.text('Parar tudo'), findsOneWidget);
      await tester.tap(find.text('Usar'));
      await tester.pumpAndSettle();
      expect(engine.status['useMode'], true);
      expect(tester.takeException(), isNull);
      await tester.tap(find.text('Parar tudo'));
      await tester.pumpAndSettle();
      expect(engine.sent, contains('stop'));
    });
  }
  testWidgets('Editing and saving sends an action', (tester) async {
    tester.view.physicalSize = const Size(1280, 1100);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final engine = FakeEngine();
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextField, 'Aplausos'),
      'Meu aplauso',
    );
    await tester.pump();
    expect(find.text('Alterações sem salvar'), findsOneWidget);
    await tester.ensureVisible(find.text('Salvar tecla'));
    await tester.tap(find.text('Salvar tecla'));
    await tester.pumpAndSettle();
    expect(engine.sent, contains('action'));
    expect(tester.takeException(), isNull);
  });
  testWidgets('Each key saves its own volume even with a shared action', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 1100);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final engine = FakeEngine();
    final keys = engine.config['Banks'][0]['Keys'] as List;
    keys[0]['Volume'] = 22;
    keys[1]['ActionId'] = 'a';
    keys[1]['Volume'] = 88;
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    expect(find.text('22%'), findsOneWidget);
    await tester.ensureVisible(find.text('Salvar tecla'));
    await tester.tap(find.text('Salvar tecla'));
    await tester.pumpAndSettle();
    final saved = engine.sentArgs[engine.sent.lastIndexOf('action')];
    expect(saved['volume'], 22);
    expect(keys[1]['Volume'], 88);
    expect(tester.takeException(), isNull);
  });
  testWidgets('Key artwork is rendered behind its label at 50 percent', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final root = Directory.systemTemp.createTempSync('mini32-artwork-test-');
    addTearDown(() => root.deleteSync(recursive: true));
    Directory('${root.path}${Platform.pathSeparator}icons').createSync();
    File('assets/branding/final-logo.png').copySync(
      '${root.path}${Platform.pathSeparator}icons${Platform.pathSeparator}sample.png',
    );
    final engine = FakeEngine();
    engine.dataRoot = root.path;
    (engine.config['Banks'][0]['Keys'] as List)[0]['IconFile'] = 'sample.png';
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    final artwork = tester
        .widgetList<Image>(find.byType(Image))
        .where((image) => image.image is FileImage)
        .toList();
    expect(artwork.length, 1);
    expect(artwork.single.opacity?.value, 0.5);
    expect(find.text('Aplausos'), findsWidgets);
    expect(tester.takeException(), isNull);
  });
  testWidgets('Free can replace one of 16 positions without limiting audio', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 1100);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final engine = FakeEngine();
    engine.config['FreeKeys'] = List.generate(16, (i) => i);
    (engine.config['Assets'] as List).addAll(
      List<Map<String, Object>>.generate(
        24,
        (i) => {'Id': 'audio-$i', 'Name': 'Áudio extra $i', 'Seconds': 2.0},
      ),
    );
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    expect(find.textContaining('16 teclas ativas'), findsOneWidget);
    expect(engine.assets.length, 25);
    final locked = find.text('Escolher posição').first;
    await tester.ensureVisible(locked);
    await tester.tap(locked);
    await tester.pumpAndSettle();
    expect(
      find.text(
        'A edição Free permite escolher 16 das 32 teclas. Selecione uma posição ativa para substituir. A configuração dela será preservada.',
      ),
      findsOneWidget,
    );
    await tester.tap(find.text('Tecla 01').last);
    await tester.pumpAndSettle();
    expect(engine.sent, contains('freeKey'));
    expect(engine.freeKeys.length, 16);
    expect(tester.takeException(), isNull);
  });
  testWidgets('Wallpaper is drawn at 50 percent behind the interface', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final root = Directory.systemTemp.createTempSync('mini32-wallpaper-test-');
    addTearDown(() => root.deleteSync(recursive: true));
    Directory('${root.path}${Platform.pathSeparator}wallpapers').createSync();
    File('assets/branding/final-logo.png').copySync(
      '${root.path}${Platform.pathSeparator}wallpapers${Platform.pathSeparator}theme.png',
    );
    final engine = FakeEngine();
    engine.dataRoot = root.path;
    engine.config['WallpaperFile'] = 'theme.png';
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    final artwork = tester
        .widgetList<Image>(find.byType(Image))
        .where((image) => image.image is ResizeImage)
        .toList();
    expect(artwork, isNotEmpty);
    expect(artwork.first.opacity?.value, 0.5);
    expect(tester.takeException(), isNull);
  });
  testWidgets('Right-click opens key menu without triggering it in Use mode', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final engine = FakeEngine();
    engine.status['useMode'] = true;
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    final tile = find.byType(ActionTile).first;
    final gesture = await tester.startGesture(
      tester.getCenter(tile),
      kind: PointerDeviceKind.mouse,
      buttons: kSecondaryMouseButton,
    );
    await gesture.up();
    await tester.pumpAndSettle();
    expect(find.text('Editar esta tecla'), findsOneWidget);
    expect(engine.sent, isNot(contains('press')));
    expect(tester.takeException(), isNull);
  });
  testWidgets('Long audio is assigned with editable music defaults', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final engine = FakeEngine();
    (engine.config['Assets'] as List).add({
      'Id': 'long',
      'Name': 'Faixa longa',
      'Seconds': 180.0,
    });
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Faixa longa'));
    await tester.tap(find.text('Atribuir').last);
    await tester.pumpAndSettle();
    final saved =
        engine.sentArgs[engine.sent.lastIndexOf('action')]['action'] as Map;
    expect(saved['Music'], true);
    expect(saved['Loop'], true);
    expect(saved['Volume'], 30);
    expect(saved['Behavior'], 'toggle');
    expect(tester.takeException(), isNull);
  });
  testWidgets('Live volume control sends a key-specific adjustment', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final engine = FakeEngine();
    engine.status['useMode'] = true;
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    expect(find.text('Volume da tecla durante a live'), findsOneWidget);
    final slider = find.byType(Slider).last;
    await tester.drag(slider, const Offset(-45, 0));
    await tester.pumpAndSettle();
    expect(engine.sent, contains('keyVolume'));
    final args = engine.sentArgs[engine.sent.lastIndexOf('keyVolume')];
    expect(args['index'], 0);
    expect((args['volume'] as int) < 75, true);
    expect(tester.takeException(), isNull);
  });
  testWidgets('Header waveform shows the current audio title', (tester) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final engine = FakeEngine();
    await tester.pumpWidget(Mini32App(bridge: engine, launch: false));
    await tester.pumpAndSettle();
    expect(find.byType(AudioMeter), findsOneWidget);
    expect(find.byTooltip('Nenhum áudio em reprodução'), findsOneWidget);
    engine.meter.value = LiveMeter(
      bars: List<double>.filled(48, 0.4),
      title: 'Aplausos',
      playing: 1,
    );
    await tester.pump();
    expect(find.byTooltip('Reproduzindo: Aplausos'), findsOneWidget);
    expect(find.byKey(const Key('live-waveform')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
  testWidgets('Window controls use the native channel', (tester) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final calls = <String>[];
    final messenger =
        TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger;
    messenger.setMockMethodCallHandler(EngineBridge.window, (call) async {
      calls.add(call.method);
      return call.method == 'toggleMaximize' || call.method == 'drag'
          ? false
          : null;
    });
    addTearDown(
      () => messenger.setMockMethodCallHandler(EngineBridge.window, null),
    );
    await tester.pumpWidget(Mini32App(bridge: FakeEngine(), launch: false));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Minimizar'));
    await tester.tap(find.byTooltip('Ampliar'));
    await tester.tap(find.byTooltip('Fechar para a bandeja'));
    await tester.drag(
      find.byKey(const Key('window-drag-area')),
      const Offset(25, 0),
    );
    await tester.pumpAndSettle();
    expect(
      calls,
      containsAll(['minimize', 'toggleMaximize', 'closeWindow', 'drag']),
    );
    expect(tester.takeException(), isNull);
  });
  testWidgets(
    'Local TT Interphases Pro trial preview loads without bundling fonts',
    (tester) async {
      tester.view.physicalSize = const Size(1280, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final loaded = await tester.runAsync(
        () => loadInterphasesPreview('../../tt_interphases_pro'),
      );
      expect(loaded, true);
      final capture = GlobalKey();
      await tester.pumpWidget(
        RepaintBoundary(
          key: capture,
          child: Mini32App(
            bridge: FakeEngine(),
            launch: false,
            fontFamily: interphasesPreviewFamily,
          ),
        ),
      );
      await tester.pumpAndSettle();
      final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
      expect(
        app.theme!.textTheme.bodyMedium!.fontFamily,
        interphasesPreviewFamily,
      );
      await tester.runAsync(() async {
        final boundary =
            capture.currentContext!.findRenderObject()!
                as RenderRepaintBoundary;
        final image = await boundary.toImage();
        final png = await image.toByteData(format: ui.ImageByteFormat.png);
        await Directory('build/test-screenshots').create(recursive: true);
        await File(
          'build/test-screenshots/font-preview.png',
        ).writeAsBytes(png!.buffer.asUint8List());
        image.dispose();
      });
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets('Package font loader can read the eight bundled faces', (
    tester,
  ) async {
    final folder = Directory.systemTemp.createTempSync('mini32-font-loader-');
    addTearDown(() => folder.deleteSync(recursive: true));
    const fixtures = {
      'Regular': 'roboto-regular.ttf',
      'Italic': 'roboto-italic.ttf',
      'Medium': 'roboto-medium.ttf',
      'Medium Italic': 'roboto-mediumitalic.ttf',
      'DemiBold': 'roboto-bold.ttf',
      'DemiBold Italic': 'roboto-bolditalic.ttf',
      'Bold': 'roboto-bold.ttf',
      'Bold Italic': 'roboto-bolditalic.ttf',
    };
    for (final entry in fixtures.entries) {
      File('assets/fonts/${entry.value}').copySync(
        '${folder.path}${Platform.pathSeparator}TT Interphases Pro ${entry.key}.ttf',
      );
    }
    final loaded = await tester.runAsync(
      () => loadLicensedInterphases(folder.path),
    );
    expect(loaded, true);
    await tester.pumpWidget(
      Mini32App(
        bridge: FakeEngine(),
        launch: false,
        fontFamily: interphasesLicensedFamily,
      ),
    );
    await tester.pumpAndSettle();
    final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
    expect(
      app.theme!.textTheme.bodyMedium!.fontFamily,
      interphasesLicensedFamily,
    );
    expect(tester.takeException(), isNull);
  });
}
