import 'package:flutter_riverpod/flutter_riverpod.dart';

/// RondiTrack sealed declaration.
sealed class ViewState<T> { const ViewState(); }
final class ViewLoading<T> extends ViewState<T> { const new(); }
final class ViewEmpty<T> extends ViewState<T> { const ViewEmpty(); }
final class ViewData<T> extends ViewState<T> {
  /// RondiTrack const declaration.
  const ViewData(this.value);
  /// RondiTrack final declaration.
  final T value;
}
final class ViewFailure<T> extends ViewState<T> {
  /// RondiTrack const declaration.
  const ViewFailure(this.message);
  /// RondiTrack final declaration.
  final String message;
}

/// RondiTrack ViewState declaration.
ViewState<List<T>> listViewState<T>(AsyncValue<List<T>> state) => switch (state) {
  /// RondiTrack AsyncData declaration.
  AsyncData(:final value) => value.isEmpty ? ViewEmpty<List<T>>() : ViewData<List<T>>(value),
  /// RondiTrack AsyncError declaration.
  AsyncError(:final error) => ViewFailure<List<T>>(error.toString()),
  _ => ViewLoading<List<T>>(),
};
