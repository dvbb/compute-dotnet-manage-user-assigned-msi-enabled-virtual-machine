---
page_type: sample
languages:
- csharp
products:
- azure
extensions:
  services: Compute
  platforms: dotnet
---

# Getting started on managing a virtual machine with User Assigned MSI using C# #

 Azure Compute sample for managing virtual machines -
  - Create a Resource Group and User Assigned MSI with CONTRIBUTOR access to the resource group
  - Create a Linux VM and associate it with User Assigned MSI
      - Install Java8, Maven3 and GIT on the VM using Azure Custom Script Extension
  - Run Java application in the MSI enabled Linux VM which uses MSI credentials to manage Azure resource
  - Retrieve the Virtual machine created from the MSI enabled Linux VM.


## Running this Sample ##

To run this sample:

Set the environment variable `CLIENT_ID`,`CLIENT_SECRET`,`TENANT_ID`,`SUBSCRIPTION_ID` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-libraries-for-net/blob/master/AUTH.md).

    git clone https://github.com/Azure-Samples/compute-dotnet-manage-user-assigned-msi-enabled-virtual-machine.git

    cd compute-dotnet-manage-user-assigned-msi-enabled-virtual-machine

    dotnet build

    bin\Debug\net452\ManageUserAssignedMSIEnabledVirtualMachine.exe

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/tree)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.