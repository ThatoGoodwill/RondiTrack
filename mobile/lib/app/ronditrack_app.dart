import 'package:flutter/material.dart';
import 'package:ronditrack_mobile/lib/core/config/app_config.dart';
import 'package:ronditrack_mobile/lib/features/home/presentation/home_screen.dart';

class RondiTrackApp extends StatelessWidget {
  /// Documented RondiTrack RondiTrackApp behavior.
  const RondiTrackApp({required this.config, super.key});
  /// Documented RondiTrack AppConfig behavior.
  final AppConfig config;

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'RondiTrack ' + config.flavor,
    theme: ThemeData(colorSchemeSeed: Colors.teal, useMaterial3: true),
    home: const HomeScreen(),
  );
}
