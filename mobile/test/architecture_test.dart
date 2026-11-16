import 'dart:io';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('screens never import data implementations, Dio or Drift, or use Navigator.push', () {
    final screens = Directory('lib/features').listSync(recursive: true)
      .whereType<File>().where((file) => file.path.endsWith('_screen.dart') &&
        file.path.replaceAll('\\', '/').contains('/presentation/'));
    for (final screen in screens) {
      final contents = screen.readAsStringSync();
      expect(RegExp("import ['\"][^'\"]*/data/").hasMatch(contents), isFalse,
        reason: screen.path);
      expect(contents.contains('Navigator.push'), isFalse, reason: screen.path);
      expect(contents.contains('package:dio/'), isFalse, reason: screen.path);
      expect(contents.contains('package:drift/'), isFalse, reason: screen.path);
      expect(RegExp(r'Color\(0x').hasMatch(contents), isFalse, reason: screen.path);
    }
  });
}
