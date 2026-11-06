import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

class LiveMeter {
  final List<double> bars;
  final String title;
  final int playing;
  const LiveMeter({this.bars = const [], this.title = '', this.playing = 0});
}

class EngineBridge extends ChangeNotifier {
  EngineBridge({this.testDataRoot});
  final String? testDataRoot;
  Map<String, dynamic> config = {}, status = {}, devices = {};
  List<String> scenes = [], inputs = [];
  final ValueNotifier<LiveMeter> meter = ValueNotifier(const LiveMeter());
  String? error;
  String dataRoot = '';
  int selected = 0;
  ValueChanged<int>? onPhysicalSelect;
  bool connected = false;
  Process? _process;
  int _sequence = 0;
  bool _closing = false;
  final _pending = <int, Completer<dynamic>>{};
  static const window = MethodChannel('mini32/window');

  Future<void> start() async {
    try {
      const configured = String.fromEnvironment('ENGINE_PATH');
      final executable = configured.isNotEmpty
          ? configured
          : '${File(Platform.resolvedExecutable).parent.path}/engine/Mini32.Engine.exe';
      if (!File(executable).existsSync())
        throw StateError(
          'Motor não encontrado. Abra pelo Iniciar.cmd da pasta do aplicativo.',
        );
      _process = await Process.start(executable, [
        '--owner-pid',
        '$pid',
        if (testDataRoot != null) ...['--data-dir', testDataRoot!],
      ], workingDirectory: File(executable).parent.path);
      _process!.stdout
          .transform(utf8.decoder)
          .transform(const LineSplitter())
          .listen(_receive, onError: (Object e) => fail('$e'));
      _process!.stderr.transform(utf8.decoder).listen((text) {
        if (text.trim().isNotEmpty) fail(text);
      });
      unawaited(
        _process!.exitCode.then((code) {
          connected = false;
          unawaited(
            window.invokeMethod<void>('background', false).catchError((_) {}),
          );
          for (final p in _pending.values) {
            if (!p.isCompleted) p.completeError(StateError('Motor encerrado.'));
          }
          _pending.clear();
          if (!_closing) {
            unawaited(window.invokeMethod<void>('show').catchError((_) {}));
            fail(
              error ??
                  'O motor encerrou (código $code). Feche e abra o aplicativo novamente.',
            );
          }
        }),
      );
    } catch (e) {
      fail('$e');
    }
  }

  void fail(String text) {
    error = text;
    notifyListeners();
  }

  void _receive(String line) {
    try {
      final message = jsonDecode(line) as Map<String, dynamic>;
      final data = message['data'];
      switch (message['type']) {
        case 'ready':
          connected = true;
          dataRoot = data['root'];
          unawaited(
            window.invokeMethod<void>('background', true).catchError((_) {}),
          );
        case 'config':
          config = Map<String, dynamic>.from(data);
        case 'status':
          status = Map<String, dynamic>.from(data);
        case 'meter':
          meter.value = LiveMeter(
            bars: (data['bars'] as List)
                .map((x) => (x as num).toDouble())
                .toList(),
            title: '${data['title'] ?? ''}',
            playing: (data['playing'] as num).toInt(),
          );
          return;
        case 'devices':
          devices = Map<String, dynamic>.from(data);
        case 'targets':
          scenes = List<String>.from(data['scenes']);
          inputs = List<String>.from(data['inputs']);
        case 'select':
          if (onPhysicalSelect != null) {
            onPhysicalSelect!(data['index']);
          } else {
            selected = data['index'];
          }
        case 'show':
          unawaited(window.invokeMethod<void>('show').catchError((_) {}));
        case 'exit':
          unawaited(close());
        case 'fatal':
          fail('$data');
        case 'reply':
          final reply = _pending.remove(data['id']);
          if (reply != null && !reply.isCompleted) {
            if (data['ok'] == true) {
              reply.complete(data['result']);
            } else {
              reply.completeError(StateError('${data['error']}'));
            }
          }
      }
      notifyListeners();
    } catch (e) {
      fail('Falha na comunicação com o motor: $e');
    }
  }

  Future<dynamic> command(
    String op, [
    Map<String, dynamic> args = const {},
  ]) async {
    if (_process == null || !connected) throw StateError('Motor desconectado.');
    final id = ++_sequence;
    final done = Completer<dynamic>();
    _pending[id] = done;
    try {
      _process!.stdin.writeln(jsonEncode({'id': id, 'op': op, ...args}));
      await _process!.stdin.flush();
      return await done.future.timeout(
        Duration(seconds: op == 'import' ? 600 : 30),
      );
    } finally {
      _pending.remove(id);
    }
  }

  Future<void> send(String op, [Map<String, dynamic> args = const {}]) async {
    try {
      await command(op, args);
    } catch (e) {
      fail('$e');
    }
  }

  Future<void> close() async {
    if (_closing) return;
    _closing = true;
    try {
      if (connected) await command('exit');
    } catch (_) {}
    await _process?.stdin.close();
    try {
      await window.invokeMethod<void>('exit');
    } catch (_) {
      exit(0);
    }
  }

  List<Map<String, dynamic>> get banks => list(config['Banks']);
  List<Map<String, dynamic>> get assets => list(config['Assets']);
  List<Map<String, dynamic>> get actions => list(config['Actions']);
  bool get isFree => status['edition'] != 'pro';
  List<int> get freeKeys => (config['FreeKeys'] as List?)?.cast<int>() ?? List.generate(16, (i) => i);
  bool keyAvailable(int index) => !isFree || freeKeys.contains(index);
  Map<String, dynamic>? get active =>
      banks.where((b) => b['Id'] == config['ActiveBank']).firstOrNull;
  Map<String, dynamic>? actionAt(int index) {
    final id = active?['Keys'][index]['ActionId'];
    return actions.where((a) => a['Id'] == id).firstOrNull;
  }

  static List<Map<String, dynamic>> list(dynamic value) => value == null
      ? []
      : (value as List).map((x) => Map<String, dynamic>.from(x)).toList();
}
