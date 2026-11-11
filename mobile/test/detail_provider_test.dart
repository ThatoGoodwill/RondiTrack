import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/home/data/fake_home_repository.dart';
import 'package:ronditrack_mobile/features/home/presentation/providers.dart';

void main() {
  test('the family isolates identities, and missing stokvels have one error', () async {
    final container = ProviderContainer(
      overrides: [homeRepositoryProvider.overrideWithValue(
        const FakeHomeRepository(delay: Duration.zero))],
    );
    addTearDown(container.dispose);
    // autoDispose providers need an active subscriber while the future resolves.
    final ubuntu = stokvelDetailsProvider('ubuntu');
    final grocery = stokvelDetailsProvider('grocery');
    final wrong = stokvelDetailsProvider('wrong');
    final subscriptions = [
      container.listen(ubuntu, (_, _) {}),
      container.listen(grocery, (_, _) {}),
      container.listen(wrong, (_, _) {}),
    ];
    addTearDown(() {
      for (final subscription in subscriptions) { subscription.close(); }
    });
    final first = await container.read(ubuntu.future);
    final second = await container.read(grocery.future);
    expect(first.home.name, 'Ubuntu Savers');
    expect(second.home.name, 'Umoja Grocery Club');
    await expectLater(container.read(wrong.future), throwsA(isA<StokvelNotFound>()));
  });
}
