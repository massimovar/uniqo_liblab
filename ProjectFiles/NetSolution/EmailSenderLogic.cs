#region Using directives
using System.Net.Mail;
using System.Net;
using QPlatform.Core;
using UAManagedCore;
using QPlatform.NetLogic;
using System.Collections.Generic;
using System.Linq;
using System;
using QPlatform.HMIProject;
#endregion

public class EmailSenderLogic : BaseNetLogic
{
    public override void Start()
    {
        ValidateCertificate();
        emailStatus = GetVariableValue("EmailSendingStatus");
        maxDelay = GetVariableValue("DelayBeforeRetry");
        maxDelay.VariableChange += RestartPeriodicTask;
    }

    private void RestartPeriodicTask(object sender, VariableChangeEventArgs e)
    {
        if (e.NewValue < 10000 || e.NewValue == null)
        {
            Log.Warning("EmailSenderLogic", "Minimum delay before retrying should be 10 seconds");
            return;
        }

        retryPeriodicTask?.Cancel();
        retryPeriodicTask = new PeriodicTask(SendQueuedMessage, e.NewValue, LogicObject);
        retryPeriodicTask.Start();
    }

    [ExportMethod]
    public void SendEmail(string emails, string emailSubject, string emailBody, string groupsList, string usersList, string userEmailLabel)
    {
        if (!IsEmailLabelValid(userEmailLabel)) return;
        if (!InitializeAndValidateSMTPParameters()) return;
        if (!ValidateEmailContent(emailSubject, emailBody)) return;

        var groupsEmails = GetGroupsEmails(groupsList, userEmailLabel);
        var usersEmails = GetUsersMails(usersList, userEmailLabel);
        var allEmails = GetSplittedItems(emails);
        allEmails = allEmails.Concat(groupsEmails).Concat(usersEmails);

        var fromAddress = CreateMailAddress(senderAddress, senderAddress);
        var toAddresses = allEmails
            .Where(m => ValidateEmailAddress(m))
            .Select(m => CreateMailAddress(m, m))
            .ToList();

        if (toAddresses.Count() == 0) {
            Log.Warning("No recipients set!");
            return;
        }

        if (retryPeriodicTask == null)
        {
            var delayBeforeRetry = GetVariableValue("DelayBeforeRetry").Value;
            if (delayBeforeRetry >= 10000)
            {
                retryPeriodicTask = new PeriodicTask(SendQueuedMessage, delayBeforeRetry, LogicObject);
                retryPeriodicTask.Start();
            }
        }

        smtpClient = new SmtpClient
        {
            Host = smtpHostname,
            Port = smtpPort,
            EnableSsl = enableSSL,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(fromAddress.Address, senderPassword)
        };

        var message = CreateEmailMessage(fromAddress, toAddresses, emailBody, emailSubject);
        TrySendEmail(message);
    }

    private bool IsEmailLabelValid(string userEmailLabel)
    {
        if (!String.IsNullOrEmpty(userEmailLabel)) return true;
        Log.Error("Please set a valid user email label");
        return false;
    }

    private IEnumerable<string> GetSplittedItems(string rawInput) => rawInput.Split(',', ';', '\t').Distinct().Select(item => item.Trim()).ToList();

    private IEnumerable<string> GetGroupsEmails(string groupsList, string userEmailVariableBrowsename)
    {
        try
        {
            var groupsLabels = GetSplittedItems(groupsList);
            var groupsNodesIds = GetNodesIntoFolder<Group>("Security")
                        .Where(g => groupsLabels.Contains(g.BrowseName))
                        .Select(g => g.NodeId)
                        .Distinct();
            var groupsEmails = GetNodesIntoFolder<User>("Security")
                        .Where(u => UserHasAtLeastOneGroup(u, groupsNodesIds)) 
                        .Select(u => GetUsersMail(u, userEmailVariableBrowsename))
                        .Where(email => ValidateEmailAddress(email))
                        .Distinct()
                        .ToList();
            return groupsEmails;
        }
        catch (System.Exception ex)
        {
            Log.Error("GroupsMails method: " + ex.Message);
            throw;
        }
    }

    private IEnumerable<string> GetUsersMails(string usersList, string userEmailVariableBrowsename)
    {
        try
        {
            var usersItems = GetSplittedItems(usersList);
            var usersEmails = GetNodesIntoFolder<User>("Security")
                        .Where(u => usersItems.Contains(u.BrowseName))
                        .Select(u => GetUsersMail(u, userEmailVariableBrowsename))
                        .Where(email => ValidateEmailAddress(email))
                        .ToList();
            return usersEmails;
        }
        catch (System.Exception ex)
        {
            Log.Error("GroupsMails method: " + ex.Message);
            throw;
        }
    }

    private string GetUsersMail(User user, string userEmailVariableBrowsename)
    {
        var userEmail = user.GetVariable(userEmailVariableBrowsename);
        if (userEmail == null) {
            Log.Error("Please check the user email label: '" + userEmailVariableBrowsename + "' does not exists!");
            return string.Empty;
        }
        return userEmail.Value.Value.ToString();
    }

    private static IEnumerable<T> GetNodesIntoFolder<T>(string rootFolder)
    {
        var objectsInFolder = new List<T>();
        foreach (var o in Project.Current.GetObject(rootFolder).Children)
        {
            switch (o)
            {
                case T _:
                    objectsInFolder.Add((T)o);
                    break;
                case Folder _:
                case UAObject _:
                    objectsInFolder.AddRange(GetNodesIntoFolder<T>(rootFolder + "/" + o.BrowseName));
                    break;
                default:
                    break;
            }
        }
        return objectsInFolder;
    }

    private bool UserHasAtLeastOneGroup(User user, IEnumerable<NodeId> groupNodeIds)
    {
        if (user == null)
            return false;
        var userGroups = user.Refs.GetObjects(QPlatform.Core.ReferenceTypes.HasGroup, false);
        foreach (var userGroup in userGroups)
        {
            if (groupNodeIds.Contains(userGroup.NodeId))
                return true;
        }
        return false;
    }

    private string ClearNodePath(string path)
    {
        var projectName = Project.Current.BrowseName + "/";
        var occurrence = path.IndexOf(projectName);
        if (occurrence == -1) { return path; }
        return path.Substring(occurrence + projectName.Length);
    }

    private MailAddress CreateMailAddress(string email, string name)
    {
        try
        {
            return new MailAddress(email, name);
        }
        catch (FormatException fex)
        {
            Log.Error("CreateMailAddress: " + email + " name: " + name + "Error: " + fex.Message);
            return null;
        }
    }

    private MailMessageWithRetries CreateEmailMessage(MailAddress fromAddress, IEnumerable<MailAddress> toAddresses, string mailBody, string mailSubject)
    {
        var mailMessage = new MailMessageWithRetries()
        {
            From = fromAddress,
            Body = mailBody,
            Subject = mailSubject,
            BodyEncoding = System.Text.Encoding.UTF8,
        };

        var attachment = GetVariableValue("Attachment").Value;
        if (!string.IsNullOrEmpty(attachment))
        {
            var attachmentUri = new ResourceUri(attachment);
            mailMessage.Attachments.Add(new Attachment(attachmentUri.Uri));
        }

        var addresses = string.Join(",", toAddresses);
        mailMessage.To.Add(addresses);
        return mailMessage;
    }

    private void TrySendEmail(MailMessageWithRetries message)
    {
        if (!CanRetrySendingMessage(message))
            return;

        using (message)
        {
            try
            {
                message.AttemptNumber++;
                Log.Info("EmailSender", $"Sending Email... ");
                smtpClient.Send(message);

                emailStatus.Value = true;
                Log.Info("EmailSenderLogic", "Email sent successfully to: " + string.Join(",", message.To.Select(m => m.Address)));
            }
            catch (SmtpException e)
            {
                emailStatus.Value = false;
                Log.Error("EmailSenderLogic", $"Email failed to send: {e.StatusCode} {e.Message}");

                if (CanRetrySendingMessage(message))
                    EnqueueFailedMessage(message);
            }
        }
    }

    private void SendQueuedMessage(PeriodicTask task)
    {
        if (failedMessagesQueue.Count == 0 || task.IsCancellationRequested)
            return;

        var message = failedMessagesQueue.Pop();

        if (CanRetrySendingMessage(message))
        {
            var retries = GetVariableValue("MaxRetriesOnFailure").Value;
            Log.Info($"Retry Sending email attempt {message.AttemptNumber} of {retries}");
            TrySendEmail(message);
        }
    }

    private void EnqueueFailedMessage(MailMessageWithRetries message)
    {
        failedMessagesQueue.Push(message);
    }

    private bool InitializeAndValidateSMTPParameters()
    {
        senderAddress = (string)GetVariableValue("SenderEmailAddress").Value;
        if (string.IsNullOrEmpty(senderAddress))
        {
            Log.Error("EmailSenderLogic", "Invalid Sender Email address");
            return false;
        }

        senderPassword = (string)GetVariableValue("SenderEmailPassword").Value;
        if (string.IsNullOrEmpty(senderPassword))
        {
            Log.Error("EmailSenderLogic", "Invalid sender password");
            return false;
        }

        smtpHostname = (string)GetVariableValue("SMTPHostname").Value;
        if (string.IsNullOrEmpty(smtpHostname))
        {
            Log.Error("EmailSenderLogic", "Invalid SMTP hostname");
            return false;
        }

        smtpPort = (int)GetVariableValue("SMTPPort").Value;
        enableSSL = (bool)GetVariableValue("EnableSSL").Value;

        return true;
    }

    private bool CanRetrySendingMessage(MailMessageWithRetries message)
    {
        var maxRetries = GetVariableValue("MaxRetriesOnFailure").Value;
        return maxRetries >= 0 && message.AttemptNumber <= maxRetries;
    }

    private class MailMessageWithRetries : MailMessage
    {
        public MailMessageWithRetries() : base() { }

        public int AttemptNumber { get; set; } = 0;
    }

    private IUAVariable GetVariableValue(string variableName)
    {
        var variable = LogicObject.GetVariable(variableName);
        if (variable == null)
        {
            Log.Error($"{variableName} not found");
            return null;
        }
        return variable;
    }

    private bool ValidateEmailAddress(string recieverEmail)
    {
        if (string.IsNullOrEmpty(recieverEmail))
        {
            Log.Error("EmailSenderLogic", "RecieverEmail is empty or null: " + recieverEmail);
            return false;
        }
        return true;
    }

    private bool ValidateEmailContent(string emailSubject, string emailBody)
    {
        if (string.IsNullOrEmpty(emailSubject))
        {
            Log.Error("EmailSenderLogic", "Email subject is empty or malformed");
            return false;
        }

        if (string.IsNullOrEmpty(emailBody))
        {
            Log.Error("EmailSenderLogic", "Email body is empty or malformed");
            return false;
        }

        return true;
    }

    private void ValidateCertificate()
    {
        if (System.Runtime.InteropServices.RuntimeInformation
                                               .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            ServicePointManager.ServerCertificateValidationCallback = (_, __, ___, ____) => { return true; };
    }

    private string senderAddress;
    private string senderPassword;
    private string smtpHostname;
    private int smtpPort;
    private bool enableSSL;

    private SmtpClient smtpClient;
    private PeriodicTask retryPeriodicTask;
    private IUAVariable maxDelay;
    private IUAVariable emailStatus;
    private readonly Stack<MailMessageWithRetries> failedMessagesQueue = new Stack<MailMessageWithRetries>();
}
