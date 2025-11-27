using Godot;
using System;

public partial class ClassCard : PanelContainer
{
   [Signal] public delegate void ClassSelectedEventHandler(ClassData data);

   private ClassData _data;
   private Label _nameLabel;
   private TextureRect _iconRect;
   private Button _btn;

   public override void _Ready()
   {
      _nameLabel = GetNode<Label>("VBoxContainer/NameLabel");
      _iconRect = GetNode<TextureRect>("VBoxContainer/Icon");
      _btn = GetNode<Button>("Button");

      _btn.Pressed += OnPressed;
   }

   public void Setup(ClassData data)
   {
      _data = data;
      _nameLabel.Text = data.ClassName;
      _iconRect.Texture = new PlaceholderTexture2D();
   }

   private void OnPressed()
   {
      EmitSignal(SignalName.ClassSelected, _data);
   }

   public void SetHighLight(bool active)
   {
      var style = GetThemeStylebox("panel") as StyleBoxFlat;
      if (style != null)
      {
         style.BorderColor = active ? Colors.Yellow : Colors.Gray;
         style.BorderWidthLeft = active ? 4 : 0;
         style.BorderWidthTop = active ? 4 : 0;
         style.BorderWidthRight = active ? 4 : 0;
         style.BorderWidthBottom = active ? 4 : 0;
      }
   }
}
