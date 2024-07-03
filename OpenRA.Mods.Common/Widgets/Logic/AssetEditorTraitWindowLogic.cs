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
		readonly HashSet<ButtonWidget> buttonFields = new();

		[ObjectCreator.UseCtor]
		public AssetEditorTraitWindowLogic(Widget widget, Action onExit, MiniYamlNode traitNode, ActorInfo actor,
			ModData modData, WorldRenderer worldRenderer)
		{
			stringOptionTemplate = widget.Get("EDITOR_SCROLLPANEL").Get("OPTION_TEMPLATE").Get("STRING_OPTION_TEMPLATE");
			foreach (var fieldNode in traitNode.Value.Nodes)
			{
				var traitNodeText = traitNode.Value.Nodes.ToLines().Prepend(traitNode.Key);
				var w = CreateFieldWidget(traitNodeText.Count(), stringOptionTemplate,
								SetEditorFieldsInner(fieldNode, value =>
									actor.EditTrait(modData.ObjectCreator, fieldNode.Key, value, ActorInfo.RulesType.Unresolved)));
			}

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


		static Widget CreateFieldWidget(int lineCount, Widget templateWidget, Widget w)
		{
			var template = templateWidget.Clone();
			var height = w.Bounds.Y + w.Bounds.Height * lineCount;
			template.Bounds = new WidgetBounds(template.Bounds.X, template.Bounds.Y, template.Bounds.Width, template.Bounds.Height + height);
			template.AddChild(w);
			return template;
		}

		void SetUpButtonFieldNew(int lineCount, ButtonWidget buttonField, string initialValue, Action<string> action)
		{
			buttonField.Bounds.Height *= lineCount;
			buttonField.GetText = () => initialValue;

			buttonField.OnClick = () => action(buttonField.GetText());
			buttonFields.Add(buttonField);
		}

		Widget SetEditorFieldsInner(MiniYamlNode trait, Action<string> action)
		{
			var template = stringOptionTemplate.Clone();
			var traitNodesText = trait.Value.Nodes.ToLines().Prepend(trait.Key);
			SetUpButtonFieldNew(traitNodesText.Count(), template.Get<ButtonWidget>("VALUE"),
				string.Join("\n", traitNodesText), x => action(x));
			return template;
		}
	}
}
