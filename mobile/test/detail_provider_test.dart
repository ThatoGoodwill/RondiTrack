import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/home/data/fake_home_repository.dart';
import 'package:ronditrack_mobile/features/home/presentation/providers.dart';

void main() {
  test('family keys keep separate stokvels and refuse a missing membership', () async {
    final container = ProviderContainer(
      overrides: [homeRepositoryProvider.overrideWithValue(
        const FakeHomeRepository(delay: Duration.zero))],
    );
    addTearDown(container.dispose);
    final a = await container.read(stokvelDetailsProvider('ubuntu').future);
    final b = await container.read(stokvelDetailsProvider('grocery').future);
    expect(a.home.name, 'Ubuntu Savers');
    expect(b.home.name, 'Umoja Grocery Club');
    await expectLater(container.read(stokvelDetailsProvider('wrong').future), throwsException);
  });
}
