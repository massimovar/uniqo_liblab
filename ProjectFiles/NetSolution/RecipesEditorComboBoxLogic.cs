#region Using directives
using System;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.OPCUAServer;
using QPlatform.Recipe;
using QPlatform.UI;
using QPlatform.Store;
using QPlatform.SQLiteStore;
using QPlatform.CommunicationDriver;
using QPlatform.Modbus;
#endregion

public class RecipesEditorComboBoxLogic : BaseNetLogic
{
	public override void Start()
	{
		var comboBox = (ComboBox)Owner;
		comboBox.SelectedValueVariable.VariableChange += SelectedValueVariable_VariableChange;

		var lastSelectedRecipeName = Owner.Owner.GetVariable("LastSelectedRecipeName")?.Value;
		if (lastSelectedRecipeName == null) return;

		var lastSelectedRecipe = lastSelectedRecipeName.Value as Object;
		var lastSelectedRecipeLT = lastSelectedRecipe as LocalizedText;
		
		if (string.IsNullOrEmpty(lastSelectedRecipeLT.Text) || lastSelectedRecipeLT.Text == "0") {
			SelectedValueVariable_VariableChange(null, null);
			return;
		}

		comboBox.SelectedValue = lastSelectedRecipeLT;
	}

	private void SelectedValueVariable_VariableChange(object sender, VariableChangeEventArgs e)
	{
		var comboBox = (ComboBox)Owner;
		if (comboBox.SelectedValue == null)
			return;

		var recipeSchemaEditor = Owner.Owner;
		var recipeSchemaVariable = recipeSchemaEditor.GetVariable("RecipeSchema");
		if (recipeSchemaVariable == null)
			return;

		var recipeSchemaNodeId = (NodeId)recipeSchemaVariable.Value.Value;

		var recipeSchemaObject = (RecipeSchema)InformationModel.Get(recipeSchemaNodeId);
		if (recipeSchemaObject == null)
			return;

		var editModelNode = recipeSchemaObject.GetObject("EditModel");
		if (editModelNode == null)
			return;

		var recipeNameLocalizedText = comboBox.SelectedValue as LocalizedText;
		if (recipeNameLocalizedText == null || recipeNameLocalizedText.IsEmpty())
			return;

		var recipeName = recipeNameLocalizedText.Text;

		recipeSchemaObject.CopyFromStoreRecipe(recipeName, editModelNode.NodeId, CopyErrorPolicy.BestEffortCopy);
	}

	public override void Stop()
	{
		var comboBox = (ComboBox)Owner;
		comboBox.SelectedValueVariable.VariableChange -= SelectedValueVariable_VariableChange;
	}
}
