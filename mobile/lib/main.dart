import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'app/ronditrack_app.dart';
import 'core/config/app_config.dart';
import 'features/home/domain/not_found.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  final config = AppConfig.load();
  runApp(ProviderScope(
    retry: (attempt, error) => error is StokvelNotFound ? null
      : (attempt >= 2 ? null : Duration(milliseconds: 200 * (attempt + 1))),
    child: RondiTrackApp(config: config)));
}
