using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Binding = System.Windows.Data.Binding;

namespace DesktopPet.Localization;

/// <summary>
/// <c>Text="{l:Tr Ctl_SkinGroup}"</c> — a one-way binding to <see cref="Loc"/>, so the text follows language switches.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        CreateBinding(Key).ProvideValue(serviceProvider);

    public static Binding CreateBinding(string key) => new($"[{key}]")
    {
        Source = Loc.Instance,
        Mode = BindingMode.OneWay,
    };

    /// <summary>Bind a property of an element created in code (e.g. a MenuItem header).</summary>
    public static void Bind(DependencyObject target, DependencyProperty property, string key) =>
        BindingOperations.SetBinding(target, property, CreateBinding(key));
}
