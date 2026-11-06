import 'dart:io';

import 'package:flutter/services.dart';

/// Keep the supplied filenames intact when loading the project-authorized fonts.
const interphasesPreviewFamily = 'TT Interphases Pro Preview';
const interphasesLicensedFamily = 'TT Interphases Pro';

Future<bool> loadIncludedInterphases(String directory) =>
    _loadFaces(directory, interphasesLicensedFamily, const [
      'TT Interphases Pro Trial Regular.ttf',
      'TT Interphases Pro Trial Italic.ttf',
      'TT Interphases Pro Trial Medium.ttf',
      'TT Interphases Pro Trial Medium Italic.ttf',
      'TT Interphases Pro Trial DemiBold.ttf',
      'TT Interphases Pro Trial DemiBold Italic.ttf',
      'TT Interphases Pro Trial Bold.ttf',
      'TT Interphases Pro Trial Bold Italic.ttf',
    ]);

Future<bool> loadLicensedInterphases(String directory) =>
    _loadFaces(directory, interphasesLicensedFamily, const [
      'TT Interphases Pro Regular.ttf',
      'TT Interphases Pro Italic.ttf',
      'TT Interphases Pro Medium.ttf',
      'TT Interphases Pro Medium Italic.ttf',
      'TT Interphases Pro DemiBold.ttf',
      'TT Interphases Pro DemiBold Italic.ttf',
      'TT Interphases Pro Bold.ttf',
      'TT Interphases Pro Bold Italic.ttf',
    ]);

Future<bool> loadInterphasesPreview(String directory) =>
    _loadFaces(directory, interphasesPreviewFamily, const [
      'TT Interphases Pro Trial Regular.ttf',
      'TT Interphases Pro Trial Italic.ttf',
      'TT Interphases Pro Trial Medium.ttf',
      'TT Interphases Pro Trial Medium Italic.ttf',
      'TT Interphases Pro Trial DemiBold.ttf',
      'TT Interphases Pro Trial DemiBold Italic.ttf',
      'TT Interphases Pro Trial Bold.ttf',
      'TT Interphases Pro Trial Bold Italic.ttf',
    ]);

Future<bool> _loadFaces(
  String directory,
  String family,
  List<String> faces,
) async {
  final files = faces
      .map((name) => File('$directory${Platform.pathSeparator}$name'))
      .toList();
  if (files.any((file) => !file.existsSync())) return false;
  final loader = FontLoader(family);
  for (final file in files) {
    loader.addFont(
      file.readAsBytes().then((bytes) => ByteData.sublistView(bytes)),
    );
  }
  await loader.load();
  return true;
}
