#region Using directives
using System;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.UI;
using QPlatform.Recipe;
using QPlatform.OPCUAServer;
using QPlatform.Store;
using System.Timers;
using QPlatform.SQLiteStore;
using QPlatform.CommunicationDriver;
using QPlatform.Modbus;
using QPlatform.TwinCat;
using QPlatform.SerialPort;
using QPlatform.Retentivity;
#endregion

public class RecipesEditorOutputMessageLogic : BaseNetLogic
{
    public override void Start()
    {
        messageVariable = Owner.GetVariable("Message");
        if (messageVariable == null)
            throw new ArgumentNullException("Unable to find variable Message in OutputMessage label");
    }

	public override void Stop()
    {
        lock (lockObject)
        {
            task?.Dispose();
        }
	}

	[ExportMethod]
	public void SetOutputMessage(string message)
	{
        lock (lockObject)
        {
            task?.Dispose();

            messageVariable.Value = message;
            task = new DelayedTask(() => { messageVariable.Value = ""; }, 5000, LogicObject);
            task.Start();
        }
	}

	[ExportMethod]
	public void SetOutputLocalizedMessage(LocalizedText message)
	{
        SetOutputMessage(InformationModel.LookupTranslation(message).Text);
	}

	private DelayedTask task;
	private IUAVariable messageVariable;
    private object lockObject = new object();
}
