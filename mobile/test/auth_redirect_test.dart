import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/core/routing/auth_redirect.dart';

void main() {
  test('signed out deep link is remembered', () {
    expect(authRedirect(location: '/join/ABC', persona: null),
      '/sign-in?from=%2Fjoin%2FABC');
  });
  test('signed in routes are not intercepted', () {
    expect(authRedirect(location: '/activity', persona: 'lerato'), isNull);
    expect(authRedirect(location: '/sign-in', persona: 'lerato'), '/home');
  });
  test('external targets are rejected', () {
    expect(safeInternalAddress('https://evil.example'), isNull);
    expect(safeInternalAddress('//evil.example'), isNull);
    expect(safeInternalAddress('/join/ABC'), '/join/ABC');
  });
}
