import 'package:flutter/services.dart' show appFlavor;

class AppConfig {
  const AppConfig({required this.flavor, required this.apiBaseUrl});

  final String flavor;
  final Uri apiBaseUrl;

  static AppConfig fromDefines({
    required String flavor,
    required String apiBaseUrl,
    String? nativeFlavor,
  }) {
    if (flavor.isEmpty || apiBaseUrl.isEmpty) {
      throw StateError('Set --dart-define-from-file=config/<flavor>.json');
    }
    if (nativeFlavor == null || nativeFlavor != flavor) {
      throw StateError('Native flavor does not match the config flavor.');
    }
    final uri = Uri.tryParse(apiBaseUrl);
    if (uri == null || uri.host.isEmpty ||
        !(uri.scheme == 'http' || uri.scheme == 'https')) {
      throw StateError('apiBaseUrl must be an HTTP(S) URL.');
    }
    if (flavor != 'dev' && uri.scheme != 'https') {
      throw StateError('Staging and production require HTTPS.');
    }
    return AppConfig(flavor: flavor, apiBaseUrl: uri);
  }

  static AppConfig load() => fromDefines(
    flavor: const String.fromEnvironment('flavor'),
    apiBaseUrl: const String.fromEnvironment('apiBaseUrl'),
    nativeFlavor: appFlavor,
  );
}
