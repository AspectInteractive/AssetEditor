using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class AssetEditorTraitWindowLogic : ChromeLogic
	{
		readonly Widget stringOptionTemplate;
		readonly HashSet<TextFieldWidget> textFields = new();

		[ObjectCreator.UseCtor]
		public AssetEditorTraitWindowLogic(Widget widget, Action onExit, MiniYamlNode traitNode, ActorInfo actor,
			ModData modData)
		{
			stringOptionTemplate = widget.Get("EDITOR_SCROLLPANEL").Get("OPTION_TEMPLATE").Get("STRING_OPTION_TEMPLATE");

			GenerateWidgetChildren(traitNode, actor, modData);

			var closeButton = widget.GetOrNull<ButtonWidget>("CLOSE_BUTTON");
			if (closeButton != null)
			{
				closeButton.OnClick = () =>
				{
					Ui.CloseWindow();
					onExit();
				};
			}
		}

		public void GenerateWidgetChildren(MiniYamlNode traitNode, ActorInfo actor, ModData modData)
		{
			Widget w;

			foreach (var fieldNode in traitNode.Value.Nodes)
			{
				w = CreateFieldWidget(stringOptionTemplate,
							SetEditorFieldsInner(fieldNode, value =>
								actor.EditTrait(modData.ObjectCreator, fieldNode.Key, value, ActorInfo.RulesType.Unresolved)));

				if (fieldNode.Value.Nodes.Length > 0)
					GenerateWidgetChildren(fieldNode, actor, modData);
			}
		}


		static Widget CreateFieldWidget(Widget templateWidget, Widget w)
		{
			var template = templateWidget.Clone();
			var height = w.Bounds.Y + w.Bounds.Height;
			template.Bounds = new WidgetBounds(template.Bounds.X, template.Bounds.Y, template.Bounds.Width, template.Bounds.Height + height);
			template.AddChild(w);
			return template;
		}

		void SetUpTextFieldNew(TextFieldWidget textField, string initialValue, Action<string> action)
		{
			textField.Text = initialValue;
			textField.OnEnterKey = _ => { action(textField.Text); textField.YieldKeyboardFocus(); return true; };
			textFields.Add(textField);
		}

		Widget SetEditorFieldsInner(MiniYamlNode fieldNode, Action<string> action)
		{
			var template = stringOptionTemplate.Clone();
			var traitNodeText = fieldNode.Key + (fieldNode.Value.Nodes.Length == 0 ? ": " + fieldNode.Value.Value : "");
			Console.WriteLine($"traitNodeText of trait window: {traitNodeText}");
			SetUpTextFieldNew(template.Get<TextFieldWidget>("VALUE"), traitNodeText, x => action(x));
			return template;
		}
	}
}
