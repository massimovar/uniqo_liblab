#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NativeUI;
using QPlatform.HMIProject;
using QPlatform.UI;
using QPlatform.Core;
using QPlatform.CoreBase;
using QPlatform.NetLogic;
using System.Collections;
using System.Linq;
using QPlatform.OPCUAServer;
#endregion

public class MatrixManager : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void UpdateMatrix()
    {
        try
        {
            var matrixToUpdate = LogicObject.GetVariable("matrixToUpdate");
            var matrixIndex = (int) LogicObject.GetVariable("matrixIndex").Value.Value;
            var arrayIndex = (int) LogicObject.GetVariable("arrayIndex").Value.Value;
            var newValue = LogicObject.GetVariable("newValue").Value.Value;

            if (matrixToUpdate == null) throw new CoreConfigurationException("Unable to find matrixToUpdate variable");
            var matrixToUpdateValue = matrixToUpdate.Value.Value;
            if (!matrixToUpdateValue.GetType().IsArray) throw new CoreConfigurationException("matrixToUpdate is not an array");
            var matrixArray = (Array)matrixToUpdateValue;
            if (matrixArray.Rank != 2) throw new CoreConfigurationException("Only two-dimensional arrays are supported");

            matrixArray.SetValue(newValue, matrixIndex, arrayIndex);
            InformationModel.GetVariable(matrixToUpdate.NodeId).Value = new UAValue(matrixArray);
        }
        catch (System.Exception e)
        {
            Log.Error(e.Message);
        }
    }
}
