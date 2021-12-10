#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.OPCUAServer;
using QPlatform.HMIProject;
using QPlatform.UI;
using QPlatform.NativeUI;
using QPlatform.CoreBase;
using QPlatform.NetLogic;
using QPlatform.Modbus;
using QPlatform.CommunicationDriver;
using System.Linq;
using QPlatform.Core;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text;
using QPlatform.Recipe;
using QPlatform.SQLiteStore;
using QPlatform.Store;
using QPlatform.TwinCat;
using QPlatform.SerialPort;
using QPlatform.Retentivity;
using QPlatform.Datalogger;
using QPlatform.EventLogger;
using QPlatform.EthernetIP;
#endregion

public class ModbusImportExportTagsCsv : BaseNetLogic
{
    QPlatform.Modbus.Driver myDriver;
    private string csvFilePath;
    private char fieldSeparator = ' ';
    private bool wrapFields;
    private string startingComment;

    [ExportMethod]
    public void ExportTags()
    {
        bool ok = CheckParameters();
        if (!ok) return;
        GenerateCsv(GetNodesIntoFolder(ClearPathFromProjectInfo(Log.Node(myDriver))));
    }

    [ExportMethod]
    public void ImportTags()
    {
        if (!CheckParameters()) return;
        ImportFromCsv(csvFilePath);
        Log.Info(MethodBase.GetCurrentMethod().Name, $"Finished tag from csv file");
    }

    private void ImportFromCsv(string csvPath)
    {
        var fieldDelimiter = fieldSeparator;
        Log.Info(MethodBase.GetCurrentMethod().Name, $"Importing tag from csv file {@"" + csvPath + ""}");
        try
        {
            using (var reader = new StreamReader(csvPath))
            {
                var csvTagObjects = new List<CsvTagObject>();

                //read header
                var headerColumns =  reader.ReadLine().Split(fieldDelimiter).ToList();
                while (!reader.EndOfStream)
                {
                    var obj = GetDataFromCsvRow(reader.ReadLine().Split(fieldDelimiter).ToList(), headerColumns);
                    if (obj == null) { continue; }
                    csvTagObjects.Add(obj);
                }

                foreach (var item in csvTagObjects)
                {
                    addModbusTag(item);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, e.Message);
        }
    }

    private string lastStructure = "";

    private void addModbusTag(CsvTagObject tagObject)
    {
        //retrieve tag parameters
        var tagPath = tagObject.Variables.SingleOrDefault(v => v.Key == "Path").Value.ToString();
        var tagName = tagObject.Variables.SingleOrDefault(v => v.Key == "Variable Name").Value.ToString();
        var type = tagObject.Variables.SingleOrDefault(v => v.Key == "Type").Value.ToString();
        var isStructure = tagObject.Variables.SingleOrDefault(v => v.Key == "IsStructure").Value.ToString();
        var arrayElements = uint.Parse(tagObject.Variables.SingleOrDefault(v => v.Key == "Array Elements").Value.ToString());
        var arrayUpdateMode = tagObject.Variables.SingleOrDefault(v => v.Key == "Array Update Mode").Value.ToString();
        var bitOffSet = UInt32.Parse(tagObject.Variables.SingleOrDefault(v => v.Key == "BitOffSet").Value.ToString());
        var numCoil = UInt16.Parse(tagObject.Variables.SingleOrDefault(v => v.Key == "NumCoil").Value.ToString());
        var numDiscreteInput = UInt16.Parse(tagObject.Variables.SingleOrDefault(v => v.Key == "NumDiscreteInput").Value.ToString());
        var maximumLength = Int32.Parse(tagObject.Variables.SingleOrDefault(v => v.Key == "Maximum Length").Value.ToString());
        var memoryArea = tagObject.Variables.SingleOrDefault(v => v.Key == "Memory Area").Value.ToString();
        var registerNumber = UInt16.Parse(tagObject.Variables.SingleOrDefault(v => v.Key == "Register Number").Value.ToString());
        TagStructure myTagStructure = null;
        string structureName = "";
        bool firstStructureElement = true;
        if (isStructure == "1")
        {
            string[] tempPath = tagPath.Split('/');
            for (int i = 0; i < tempPath.Length - 1; i++)
            {
                if (i == 0)
                    tagPath = tempPath[i];
                else
                    tagPath = tagPath + "/" + tempPath[i];
            }
            structureName = tempPath[tempPath.Length - 1];
            if (structureName != lastStructure) 
            {
                lastStructure = structureName;
                firstStructureElement = true;
            }
            else
                firstStructureElement = false;
        }

        //create Folder for the Tag if not existing
        CreateFoldersTreeFromPath(tagPath);

        //delete if existing tag
        if (Project.Current.Get(tagPath + "/" + tagName) != null)
        Project.Current.Get(tagPath + "/" + tagName).Delete();

        if (isStructure == "1")
        {
            if (firstStructureElement)
            {
                if (Project.Current.Get(tagPath + "/" + structureName) != null)
                    Project.Current.Get(tagPath + "/" + structureName).Delete();
                myTagStructure = InformationModel.Make<QPlatform.CommunicationDriver.TagStructure>(structureName);
            }
            else
                myTagStructure = Project.Current.Get<TagStructure>(tagPath + "/" + structureName);
        }


        //create and add the tag to the porject
        QPlatform.Modbus.Tag modbusTag = null;
        NodeId typeNodeId = null; 
        switch (type)
        {
            case "Boolean":
                typeNodeId = OpcUa.DataTypes.Boolean;
                break;
            case "Int16":
                typeNodeId = OpcUa.DataTypes.Int16;
                break;
            case "Int32":
                typeNodeId = OpcUa.DataTypes.Int32;
                break;
            case "Int64":
                typeNodeId = OpcUa.DataTypes.Int64;
                break;
            case "UInt16":
                typeNodeId = OpcUa.DataTypes.UInt16;
                break;
            case "UInt32":
                typeNodeId = OpcUa.DataTypes.UInt32;
                break;
            case "UInt64":
                typeNodeId = OpcUa.DataTypes.UInt64;
                break;
            case "Float":
                typeNodeId = OpcUa.DataTypes.Float;
                break;
            case "String":
                typeNodeId = OpcUa.DataTypes.String;
                break;
            default:
                break;
        }
        
        modbusTag = InformationModel.MakeVariable<QPlatform.Modbus.Tag>(tagName, typeNodeId);
        modbusTag.BitOffset = bitOffSet;
        modbusTag.NumCoil = numCoil;
        modbusTag.NumDiscreteInput = numDiscreteInput;
        modbusTag.MaximumLength = maximumLength;
        if (arrayElements > 0)
        {
            modbusTag.NumRegister = registerNumber;
            var arrayDimensions = new uint[1];
            arrayDimensions[0] = arrayElements;
            modbusTag.ArrayDimensions = arrayDimensions;
        }
        if (arrayUpdateMode == "Element")
            modbusTag.ArrayUpdateMode = (TagArrayUpdateMode)0;
        else
            modbusTag.ArrayUpdateMode = (TagArrayUpdateMode)1;
        switch (memoryArea)
        {
            case "HoldinRegister":
                modbusTag.MemoryArea = (ModbusMemoryArea)0;
                break;
            case "Coil":
                modbusTag.MemoryArea = (ModbusMemoryArea)1;
                break;
            case "InputRegister":
                modbusTag.MemoryArea = (ModbusMemoryArea)2;
                break;
            case "DiscreteInput":
                modbusTag.MemoryArea = (ModbusMemoryArea)3;
                break;
            default:
                break;
        }
        if (isStructure == "1")
        {
            if (firstStructureElement)
            {
                myTagStructure.Add(modbusTag);
                Project.Current.Get(tagPath).Add(myTagStructure);
            }
            else
                myTagStructure.Add(modbusTag);
        } 
        else
            Project.Current.Get(tagPath).Add(modbusTag);
    }
    private static bool CreateFoldersTreeFromPath(string path)
    {

        if (string.IsNullOrEmpty(path)) { return true; }
        var segments = path.Split('/').ToList();
        string updatedSegment = "";
        string segmentsAccumulator = "";

        try
        {
            foreach (var s in segments)
            {
                if (segmentsAccumulator == "")
                    updatedSegment = s;
                else
                    updatedSegment = updatedSegment + "/" + s;
                var folder = InformationModel.MakeObject<Folder>(s);
                var folderAlreadyExists = Project.Current.GetObject(updatedSegment) != null;
                if (!folderAlreadyExists)
                {
                    if (segmentsAccumulator == "")
                        Project.Current.Add(folder);
                    else
                        Project.Current.GetObject(segmentsAccumulator).Children.Add(folder);
                }
                segmentsAccumulator = updatedSegment;
            }
        }
        catch (Exception e)
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, $"Cannot create folder, error {e.Message}");
            return false;
        }
        return true;
    }
    
    private bool CheckParameters()
    {
        //driver
        myDriver = InformationModel.Get<QPlatform.Modbus.Driver>(LogicObject.GetVariable("ModbusDriver").Value);
        if (myDriver == null)
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, "No driver set");
            return false;
        }

        //csv file path
        var csvPathVariable = LogicObject.Children.Get<IUAVariable>("CsvPath");
        csvFilePath = new ResourceUri(csvPathVariable.Value).Uri;
        if (string.IsNullOrEmpty(csvFilePath))
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, "No file path set");
            return false;
        }

        //field separator
        var separatorVariable = LogicObject.GetVariable("FieldSeparator");
        if (separatorVariable == null)
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, "Field Separator variable not found");
            return false;
        }
        string separator = separatorVariable.Value;
        if (separator.Length != 1 || separator == String.Empty)
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, "Wrong Field Separator configuration. Please insert a char");
            return false;
        }
        char.TryParse(separator, out char result);
        fieldSeparator = result;

        //wrap condition
        wrapFields = LogicObject.GetVariable("WrapFields").Value;

        //starting comment
        startingComment = (string)LogicObject.GetVariable("StartingComment").Value;

        return true;
    }

    private static ICollection<IUANode> GetNodesIntoFolder(string root)
    {
        var objectsInFolder = new List<IUANode>();
        foreach (var o in Project.Current.GetObject(root).Children)
        {
            switch (o)
            {
                case QPlatform.Modbus.Tag _:
                    objectsInFolder.Add(o);
                    break;
                case Folder _:
                case TagStructure _:
                    objectsInFolder.AddRange(GetNodesIntoFolder(root + "/" + o.BrowseName));
                    break;
                case UAObject _:
                    objectsInFolder.AddRange(GetNodesIntoFolder(root + "/" + o.BrowseName));
                    break;
                default:
                    break;
            }
        }
        return objectsInFolder;
    }

    private void GenerateCsv(ICollection<IUANode> nodes)
    {
        Log.Info(MethodBase.GetCurrentMethod().Name, $"Writing {nodes.Count} variables");
        try
        {
            using (var csvWriter = new CsvFileWriter(csvFilePath) { FieldDelimiter = fieldSeparator, WrapFields = wrapFields })
            {
                csvWriter.WriteLine(new string[] { "Variable Name", "Path", "IsStructure","Type", "Array Elements", "Array Update Mode", "Maximum Length", "Memory Area", "Register Number", "BitOffSet", "NumCoil", "NumDiscreteInput"});
                foreach (QPlatform.Modbus.Tag item in nodes)
                {
                    var varName = item.BrowseName;
                    var path = ClearPathFromProjectInfo(Log.Node(item)).Replace("/" + varName,"");
                    string isStructure = "0";
                    if (InformationModel.Get(Project.Current.Get(path).NodeId).GetType().Name == "TagStructure")
                        isStructure = "1";
                    var type = InformationModel.Get(item.DataType).BrowseName;
                    uint[] arrDim = item.ArrayDimensions;
                    uint arrayElements = 0;
                    if (arrDim.Length > 0)
                        arrayElements = arrDim[0];
                    var arrayUpdateMode = item.ArrayUpdateMode;
                    var maxLength = item.MaximumLength;
                    var memoryArea = item.MemoryArea;
                    var registerNumber = item.NumRegister;
                    var bitOffSet = item.BitOffset;
                    var numCoil = item.NumCoil;
                    var numDiscreteInput = item.NumDiscreteInput;
                    csvWriter.WriteLine(new string[] { varName, path, isStructure, type, arrayElements.ToString(), arrayUpdateMode.ToString(), maxLength.ToString(), memoryArea.ToString(), registerNumber.ToString(), bitOffSet.ToString(), numCoil.ToString(), numDiscreteInput.ToString() });

                }
            }
        }
        catch (Exception e)
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, "Unable to export node, error: " + e);
        }
    }

    private string MakeBrowsePath(IUANode node)
    {
        string path = node.BrowseName;
        var current = node.Owner;

        while (current != Project.Current)
        {
            path = current.BrowseName + "/" + path;
            current = current.Owner;
        }
        return path;
    }

    private string ClearPathFromProjectInfo(string path)
    {
        var projectName = Project.Current.BrowseName + "/";
        var occurrence = path.IndexOf(projectName);
        if (occurrence == -1) { return path; }
        path = path.Substring(occurrence + projectName.Length);
        return path;
    }

    private CsvTagObject GetDataFromCsvRow(List<string> line, List<string> header)
    {
        if (startingComment != "" && line[0].Contains(startingComment))
            return null;
        var csvTagObject = new CsvTagObject();
        for (var i = 0; i < header.Count; i++)
        {
            csvTagObject.Variables.Add(header[i], line[i]);
        }
        
        return csvTagObject;
    }

    private class CsvTagObject
    {
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
    }
    private class CsvFileWriter : IDisposable
    {
        public char FieldDelimiter { get; set; } = ',';

        public char QuoteChar { get; set; } = '"';

        public bool WrapFields { get; set; } = false;

        public CsvFileWriter(string filePath)
        {
            _streamWriter = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        }

        public CsvFileWriter(string filePath, System.Text.Encoding encoding)
        {
            _streamWriter = new StreamWriter(filePath, false, encoding);
        }

        public CsvFileWriter(StreamWriter streamWriter)
        {
            this._streamWriter = streamWriter;
        }

        public void WriteLine(string[] fields)
        {
            var stringBuilder = new StringBuilder();

            for (var i = 0; i < fields.Length; ++i)
            {
                if (WrapFields)
                    stringBuilder.AppendFormat("{0}{1}{0}", QuoteChar, EscapeField(fields[i]));
                else
                    stringBuilder.AppendFormat("{0}", fields[i]);

                if (i != fields.Length - 1)
                    stringBuilder.Append(FieldDelimiter);
            }

            _streamWriter.WriteLine(stringBuilder.ToString());
            _streamWriter.Flush();
        }

        private string EscapeField(string field)
        {
            var quoteCharString = QuoteChar.ToString();
            return field.Replace(quoteCharString, quoteCharString + quoteCharString);
        }

        private StreamWriter _streamWriter;

        #region IDisposable Support
        private bool _disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
                _streamWriter.Dispose();

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
