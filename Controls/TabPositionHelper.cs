
using System.Windows;

namespace MarvinsAIRARefactored.Controls;

public static class TabItemPositionHelper
{
	public static readonly DependencyProperty IsFirstProperty = DependencyProperty.RegisterAttached( "IsFirst", typeof( bool ), typeof( TabItemPositionHelper ), new FrameworkPropertyMetadata( false ) );

	public static bool GetIsFirst( DependencyObject obj ) => (bool) obj.GetValue( IsFirstProperty );
	public static void SetIsFirst( DependencyObject obj, bool value ) => obj.SetValue( IsFirstProperty, value );

	public static readonly DependencyProperty IsLastProperty = DependencyProperty.RegisterAttached( "IsLast", typeof( bool ), typeof( TabItemPositionHelper ), new FrameworkPropertyMetadata( false ) );

	public static bool GetIsLast( DependencyObject obj ) => (bool) obj.GetValue( IsLastProperty );
	public static void SetIsLast( DependencyObject obj, bool value ) => obj.SetValue( IsLastProperty, value );
}
