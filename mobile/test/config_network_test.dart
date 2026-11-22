import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/core/config/app_config.dart';

void main() {
  test('dev HTTP is permitted for emulator only when explicitly configured', () {
    expect(AppConfig.fromDefines(flavor: 'dev',
      apiBaseUrl: 'http://10.0.2.2:8085', nativeFlavor: 'dev').apiBaseUrl.scheme,
      'http');
  });

  test('staging and production never allow cleartext API URLs', () {
    for (final flavor in ['staging','prod']) {
      expect(() => AppConfig.fromDefines(flavor: flavor,
        apiBaseUrl: 'http://example.org', nativeFlavor: flavor),
        throwsStateError);
    }
  });
}
