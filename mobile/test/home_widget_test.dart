import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/home/data/fake_home_repository.dart';
import 'package:ronditrack_mobile/features/home/domain/home_repository.dart';
import 'package:ronditrack_mobile/features/home/domain/models.dart';
import 'package:ronditrack_mobile/features/home/presentation/home_screen.dart';
import 'package:ronditrack_mobile/features/home/presentation/providers.dart';

class FailingHomeRepository implements HomeRepository {
  @override
  Future<List<HomeEntry>> loadHome(String persona) async => throw StateError('offline');
  @override
  Future<StokvelDetails> loadDetail(String persona, String stokvelId) async =>
    throw StateError('offline');
}

void main() {
  testWidgets('home renders cards for a member who is treasurer elsewhere', (tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [homeRepositoryProvider.overrideWithValue(
        const FakeHomeRepository(delay: Duration.zero))],
      child: const MaterialApp(home: HomeScreen()),
    ));
    await tester.pumpAndSettle();
    expect(find.text('Ubuntu Savers'), findsOneWidget);
    expect(find.text('Umoja Grocery Club'), findsOneWidget);
  });
  testWidgets('home empty is an explanation rather than an error', (tester) async {
    await tester.pumpWidget(ProviderScope(
      overrides: [homeRepositoryProvider.overrideWithValue(
        const FakeHomeRepository(delay: Duration.zero))],
      child: const MaterialApp(home: HomeScreen()),
    ));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('persona-picker')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Nomsa').last);
    await tester.pumpAndSettle();
    expect(find.textContaining('do not belong to any stokvels'), findsOneWidget);
  });
  testWidgets('a repository failure offers retry', (tester) async {
    await tester.pumpWidget(ProviderScope(
      retry: (count, error) => null,
      overrides: [homeRepositoryProvider.overrideWithValue(FailingHomeRepository())],
      child: const MaterialApp(home: HomeScreen()),
    ));
    await tester.pumpAndSettle();
    expect(find.text('Try again'), findsOneWidget);
  });
}
