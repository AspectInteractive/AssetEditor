using System;
using System.Collections.Generic;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class AssetEditorTraitWindowLogic : ChromeLogic
	{
		readonly Widget container;
		readonly Widget panel;
		readonly HashSet<TextFieldWidget> textFields = new();
		readonly string traitTextFieldId = "VALUE";
		readonly WidgetBounds traitTextFieldBounds = new(10, 10, 150, 20);
		int heightInc = 20;

		[ObjectCreator.UseCtor]
		public AssetEditorTraitWindowLogic(Widget widget, Action onExit, MiniYamlNodeBuilder traitNode, ActorInfo actor,
			ModData modData)
		{
			container = widget;
			panel = widget.Get("EDITOR_SCROLLPANEL");

			CreateFieldWidget(SetEditorFieldsInner(traitNode, value =>
				{
					actor.EditTraitOrField(traitNode, value);
					actor.LoadTraits(modData.ObjectCreator, actor.ActorUnresolvedRules, true);
				}));

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

		public void GenerateWidgetChildren(MiniYamlNodeBuilder traitNode, ActorInfo actor, ModData modData)
		{
			foreach (var fieldNode in traitNode.Value.Nodes)
			{
				CreateFieldWidget(SetEditorFieldsInner(fieldNode, value =>
				{
					actor.EditTraitOrField(fieldNode, value);
					actor.LoadTraits(modData.ObjectCreator, actor.ActorUnresolvedRules, true);
				}));

				if (fieldNode.Value.Nodes.Count > 0)
					GenerateWidgetChildren(fieldNode, actor, modData);
			}
		}

		Widget CreateFieldWidget(Widget w)
		{
			var template = container;
			template.AddChild(w);
			return template;
		}

		void SetUpTextFieldNew(TextFieldWidget textField, string initialValue, Action<string> action)
		{
			textField.Text = initialValue;
			textField.OnEnterKey = _ => { action(textField.Text); textField.YieldKeyboardFocus(); return true; };
			textField.Bounds = new WidgetBounds(panel.Bounds.X + 10, textField.Bounds.Y + 10 + heightInc, textField.Bounds.Width, textField.Bounds.Height);
			heightInc += 20;
			textFields.Add(textField);
		}

		Widget SetEditorFieldsInner(MiniYamlNodeBuilder fieldNode, Action<string> action)
		{
			var template = new TextFieldWidget
			{
				Id = traitTextFieldId,
				Bounds = traitTextFieldBounds
			};

			var traitNodeText = fieldNode.Key + (fieldNode.Value.Nodes.Count == 0 ? ": " + fieldNode.Value.Value : "");
			SetUpTextFieldNew(template.Get<TextFieldWidget>("VALUE"), traitNodeText, x => action(x));
			return template;
		}
	}
}
