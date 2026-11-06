import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'bridge.dart';

/// Shows the amplitude history of the actual audio sent to the output device.
/// Only this small header region rebuilds for meter frames.
class AudioMeter extends StatelessWidget {
  final EngineBridge bridge;
  const AudioMeter({super.key, required this.bridge});

  @override
  Widget build(BuildContext context) => ValueListenableBuilder<LiveMeter>(
    valueListenable: bridge.meter,
    builder: (context, frame, _) {
      final active = frame.playing > 0;
      final description = active
          ? 'Reproduzindo: ${frame.title.isEmpty ? 'áudio' : frame.title}'
          : 'Nenhum áudio em reprodução';
      return Tooltip(
        message: description,
        child: Semantics(
          label: 'Forma de onda. $description',
          child: RepaintBoundary(
            child: Container(
              height: 39,
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
              decoration: BoxDecoration(
                color: const Color(0x80302a27),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: const Color(0x4dffad76)),
              ),
              child: CustomPaint(
                key: const Key('live-waveform'),
                painter: _WaveformPainter(frame.bars, active),
                child: const SizedBox.expand(),
              ),
            ),
          ),
        ),
      );
    },
  );
}

class _WaveformPainter extends CustomPainter {
  final List<double> bars;
  final bool active;
  const _WaveformPainter(this.bars, this.active);

  @override
  void paint(Canvas canvas, Size size) {
    if (size.width <= 0 || size.height <= 0) return;
    final mid = size.height / 2;
    final baseline = Paint()
      ..color = const Color(0x665e4740)
      ..strokeWidth = 1;
    canvas.drawLine(Offset(0, mid), Offset(size.width, mid), baseline);
    const count = 48;
    final step = size.width / count;
    final width = math.max(1.5, math.min(4.0, step * 0.48));
    for (var i = 0; i < count; i++) {
      final level = active && i < bars.length ? bars[i].clamp(0.0, 1.0) : 0.0;
      final height = active ? math.max(1.4, level * (mid - 2)) : 1.2;
      final alpha = active ? (0.38 + 0.62 * level) : 0.24;
      final paint = Paint()
        ..color = const Color(0xffffa05b).withValues(alpha: alpha);
      final rect = Rect.fromCenter(
        center: Offset((i + 0.5) * step, mid),
        width: width,
        height: height * 2,
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(rect, const Radius.circular(2)),
        paint,
      );
    }
  }

  @override
  bool shouldRepaint(covariant _WaveformPainter oldDelegate) =>
      oldDelegate.bars != bars || oldDelegate.active != active;
}
