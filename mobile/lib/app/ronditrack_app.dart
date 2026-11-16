import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../core/config/app_config.dart';
import '../core/routing/app_router.dart';
import '../core/theme/rondi_theme.dart';

class RondiTrackApp extends ConsumerWidget {
  const RondiTrackApp({required this.config, super.key});
  final AppConfig config;
  @override Widget build(BuildContext context, WidgetRef ref) => MaterialApp.router(
    title: 'RondiTrack ${config.flavor}',
    theme: rondiTheme(Brightness.light), darkTheme: rondiTheme(Brightness.dark),
    themeMode: ThemeMode.system,
    routerConfig: ref.watch(appRouterProvider(config)),
  );
}
