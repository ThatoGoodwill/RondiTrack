import 'package:flutter_riverpod/flutter_riverpod.dart';

sealed class ViewState<T> { const ViewState(); }
final class ViewLoading<T> extends ViewState<T> { const ViewLoading(); }
final class ViewEmpty<T> extends ViewState<T> { const ViewEmpty(); }
final class ViewData<T> extends ViewState<T> {
  const ViewData(this.value);
  final T value;
}
final class ViewFailure<T> extends ViewState<T> {
  const ViewFailure(this.message);
  final String message;
}

ViewState<List<T>> listViewState<T>(AsyncValue<List<T>> state) => switch (state) {
  AsyncData(:final value) => value.isEmpty ? ViewEmpty<List<T>>() : ViewData<List<T>>(value),
  AsyncError(:final error) => ViewFailure<List<T>>(error.toString()),
  _ => ViewLoading<List<T>>(),
};
