enum WindowClass { compact, medium, expanded }

WindowClass windowClass(double width) {
  if (width < 600) return WindowClass.compact;
  if (width < 840) return WindowClass.medium;
  return WindowClass.expanded;
}
