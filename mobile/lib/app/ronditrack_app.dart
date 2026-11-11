import 'package:flutter/material.dart';
import '../core/config/app_config.dart';
import '../features/home/presentation/home_screen.dart';

class RondiTrackApp extends StatelessWidget {
  const RondiTrackApp({required this.config, super.key});
  final AppConfig config;

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'RondiTrack ' + config.flavor,
    theme: ThemeData(colorSchemeSeed: Colors.teal, useMaterial3: true),
    home: const HomeScreen(),
  );
}
