using Godot;
using System;
using System.IO;

namespace ThreeKingdom.Core;

internal static class ScreenshotShortcut
{
    private const string ScreenshotDirectory = "user://screenshots";

    public static void HandleInput(Node owner, InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.Keycode != Key.F1)
        {
            return;
        }

        CaptureViewportToFile(owner);
        owner.GetViewport().SetInputAsHandled();
    }

    private static void CaptureViewportToFile(Node owner)
    {
        var image = owner.GetViewport().GetTexture().GetImage();
        if (image == null)
        {
            GD.PushWarning("Screenshot failed: viewport image is unavailable.");
            return;
        }

        EnsureDirectory(ScreenshotDirectory);
        var resourcePath = $"{ScreenshotDirectory}/screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var saveError = image.SavePng(resourcePath);
        if (saveError != Error.Ok)
        {
            GD.PushWarning($"Screenshot failed: could not save {resourcePath} ({saveError}).");
            return;
        }

        GD.Print($"Screenshot saved: {ProjectSettings.GlobalizePath(resourcePath)}");
    }

    private static void EnsureDirectory(string resourceDirectory)
    {
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(resourceDirectory));
    }
}
