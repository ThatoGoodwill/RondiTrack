import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/core/config/app_config.dart';

void main() {
  test('refuses missing config', () {
    expect(() => AppConfig.fromDefines(flavor: '', apiBaseUrl: '', nativeFlavor: null),
      throwsStateError);
  });
  test('refuses a prod app started with dev configuration', () {
    expect(() => AppConfig.fromDefines(flavor: 'dev',
      apiBaseUrl: 'http://10.0.2.2:8085', nativeFlavor: 'prod'), throwsStateError);
  });
  test('accepts matching native and Dart flavors', () {
    final dev = AppConfig.fromDefines(flavor: 'dev',
      apiBaseUrl: 'http://10.0.2.2:8085', nativeFlavor: 'dev');
    expect(dev.flavor, 'dev');
  });
}
