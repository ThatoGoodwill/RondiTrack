import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/home/presentation/view_state.dart';

void main() {
  test('the no-stokvel state is empty, not data', () {
    expect(listViewState<int>(const AsyncData([])), isA<ViewEmpty<List<int>>>());
    expect(listViewState<int>(const AsyncData([1])), isA<ViewData<List<int>>>());
    expect(listViewState<int>(const AsyncLoading()), isA<ViewLoading<List<int>>>());
    expect(listViewState<int>(AsyncError(Exception('no network'), StackTrace.empty)),
      isA<ViewFailure<List<int>>>());
  });
}
