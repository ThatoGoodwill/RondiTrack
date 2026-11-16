/// The auth gate is one pure function, testable without Flutter widgets.
/// Only internal destinations may survive the sign-in transition.
String? safeInternalAddress(String? address) {
  if (address == null || !address.startsWith('/') || address.startsWith('//') ||
      address.contains(String.fromCharCode(92)) || address.contains('://')) return null;
  final uri = Uri.tryParse(address);
  if (uri == null || uri.hasAuthority || uri.scheme.isNotEmpty ||
      uri.path == '/sign-in') return null;
  return address;
}

String? authRedirect({
  required String location,
  required String? persona,
  String? rememberedAddress,
}) {
  final signedIn = persona != null;
  final path = Uri.tryParse(location)?.path;
  final signingIn = path == '/sign-in';
  if (!signedIn && !signingIn) {
    final from = Uri.encodeComponent(safeInternalAddress(location) ?? '/home');
    return '/sign-in?from=$from';
  }
  if (signedIn && signingIn) {
    return safeInternalAddress(rememberedAddress) ?? '/home';
  }
  return null;
}
