import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'app/ronditrack_app.dart';
import 'core/config/app_config.dart';
import 'core/network/client.dart';
import 'core/network/failure.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  final config = AppConfig.load();
  runApp(ProviderScope(
    overrides: [appConfigProvider.overrideWithValue(config)],
    retry: (attempt, error) =>
      error is OfflineFailure && attempt < 2
        ? Duration(milliseconds: 250 * (attempt + 1)) : null,
    child: RondiTrackApp(config: config)));
}
