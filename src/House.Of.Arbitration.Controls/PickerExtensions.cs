using Microsoft.Maui.Handlers;
#if ANDROID || WINDOWS || IOS || MACCATALYST
using Microsoft.Maui.Platform;
#endif

namespace House.Of.Arbitration.Controls;

/// <summary>
/// Propriétés attachées pour personnaliser les contrôles <see cref="Picker"/>,
/// notamment la couleur du trait inférieur (soulignement).
/// </summary>
public static class PickerExtensions
{
    private const string UnderlineMappingKey = "PickerUnderlineColor";

    /// <summary>
    /// Couleur du trait inférieur affiché sous le <see cref="Picker"/>.
    /// Utiliser <see cref="Colors.Transparent"/> pour masquer le trait.
    /// </summary>
    public static readonly BindableProperty UnderlineColorProperty =
        BindableProperty.CreateAttached(
            "UnderlineColor",
            typeof(Color),
            typeof(PickerExtensions),
            null,
            propertyChanged: OnUnderlineColorChanged);

    public static Color? GetUnderlineColor(BindableObject view)
        => (Color?)view.GetValue(UnderlineColorProperty);

    public static void SetUnderlineColor(BindableObject view, Color? value)
        => view.SetValue(UnderlineColorProperty, value);

    private static void OnUnderlineColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is Picker picker)
        {
            picker.Handler?.UpdateValue(UnderlineMappingKey);
        }
    }

    /// <summary>
    /// Enregistre le mapping multiplateforme sur <see cref="PickerHandler.Mapper"/>
    /// afin d'appliquer <see cref="UnderlineColorProperty"/>.
    /// </summary>
    public static void ConfigureHandler()
    {
        PickerHandler.Mapper.AppendToMapping(UnderlineMappingKey, (handler, view) =>
        {
            if (view is not BindableObject bindable)
                return;

            var color = GetUnderlineColor(bindable);
            if (color == null)
                return;

#if ANDROID
            handler.PlatformView.BackgroundTintList = null;

            var layers = new List<Android.Graphics.Drawables.Drawable>();
            if (view is VisualElement ve && ve.BackgroundColor != null && ve.BackgroundColor.Alpha > 0f)
            {
                layers.Add(new Android.Graphics.Drawables.ColorDrawable(ve.BackgroundColor.ToPlatform()));
            }

            if (color.Alpha > 0f)
            {
                var lineDrawable = new Android.Graphics.Drawables.GradientDrawable();
                lineDrawable.SetShape(Android.Graphics.Drawables.ShapeType.Rectangle);
                lineDrawable.SetColor(color.ToPlatform());
                layers.Add(lineDrawable);
            }

            if (layers.Count == 0)
            {
                handler.PlatformView.Background = null;
            }
            else
            {
                var layerDrawable = new Android.Graphics.Drawables.LayerDrawable(layers.ToArray());
                if (color.Alpha > 0f)
                {
                    float density = handler.PlatformView.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
                    int lineHeightPx = Math.Max(1, (int)(1.5f * density));
                    int lineIndex = layers.Count - 1;
                    layerDrawable.SetLayerGravity(lineIndex, Android.Views.GravityFlags.Bottom | Android.Views.GravityFlags.FillHorizontal);
                    layerDrawable.SetLayerHeight(lineIndex, lineHeightPx);
                }

                handler.PlatformView.Background = layerDrawable;
            }
#elif WINDOWS
            handler.PlatformView.BorderBrush = color.ToPlatform();
            handler.PlatformView.BorderThickness = color.Alpha <= 0f
                ? new Microsoft.UI.Xaml.Thickness(0)
                : new Microsoft.UI.Xaml.Thickness(0, 0, 0, 1.5);
#elif IOS || MACCATALYST
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            const string layerName = "PickerUnderlineLayer";
            var sublayers = handler.PlatformView.Layer.Sublayers;
            if (sublayers != null)
            {
                foreach (var layer in sublayers)
                {
                    if (layer.Name == layerName)
                    {
                        layer.RemoveFromSuperLayer();
                        break;
                    }
                }
            }

            if (color.Alpha > 0f && handler.PlatformView.Frame.Width > 0)
            {
                var underline = new CoreAnimation.CALayer
                {
                    Name = layerName,
                    Frame = new CoreGraphics.CGRect(0, handler.PlatformView.Frame.Height - 1.5, handler.PlatformView.Frame.Width, 1.5),
                    BackgroundColor = color.ToCGColor()
                };
                handler.PlatformView.Layer.AddSublayer(underline);
            }
#endif
        });
    }
}
