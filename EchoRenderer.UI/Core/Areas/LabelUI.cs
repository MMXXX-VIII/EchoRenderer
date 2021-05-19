﻿using System;
using CodeHelpers.Mathematics;
using SFML.Graphics;
using SFML.System;

namespace EchoRenderer.UI.Core.Areas
{
	public class LabelUI : AreaUI
	{
		string _text;
		Text.Styles _styles;
		bool _centered = true;

		public string Text
		{
			get => _text;
			set
			{
				if (_text == value) return;
				_text = value;

				display.DisplayedString = value;
				transform.MarkDirty();
			}
		}

		public Text.Styles Styles
		{
			get => _styles;
			set
			{
				if (_styles == value) return;
				_styles = value;

				display.Style = value;
				transform.MarkDirty();
			}
		}

		public bool Centered
		{
			get => _centered;
			set => _centered = value;
		}

		public override Color FillColor
		{
			get => display.FillColor;
			set => display.FillColor = value;
		}

		readonly Text display = new Text {Font = mono, FillColor = Theme.Current.ContrastColor};
		static readonly Font mono = new Font("Assets/Fonts/JetBrainsMono/JetBrainsMono-Bold.ttf");

		public float GetPosition(int index) => display.FindCharacterPos((uint)index).X;

		protected override void Reorient(Float2 position, Float2 size)
		{
			base.Reorient(position, size);

			display.CharacterSize = (uint)Math.Max(0, size.y);

			var bounds = display.GetLocalBounds();

            Float2 center = position + (Centered ? (size / 2f) : new Float2((Text.Length / 2f) * (display.CharacterSize / 2f) + display.CharacterSize * Text.Length / 20f, 
																			size.y / 2f));

			Float2 offset = new Float2(bounds.Left, bounds.Top);
			Float2 extend = new Float2(bounds.Width, bounds.Height) / 2f;

			display.Origin = (offset + extend).As();
			display.Position = center.As();
		}

		protected override void Paint(RenderTarget renderTarget)
		{
			base.Paint(renderTarget);
			renderTarget.Draw(display);
		}
	}
}