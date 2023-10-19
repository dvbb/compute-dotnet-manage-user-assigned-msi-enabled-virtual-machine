// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using System.Net;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Compute;
using System.Net.NetworkInformation;
using System.Xml.Linq;

namespace ManageUserAssignedMSIEnabledVirtualMachine
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;
    
        /**
         * Azure Compute sample for managing virtual machines -
         *  - Create a Resource Group and User Assigned MSI with CONTRIBUTOR access to the resource group
         *  - Create a Linux VM and associate it with User Assigned MSI
         *      - Install Java8, Maven3 and GIT on the VM using Azure Custom Script Extension
         *  - Run Java application in the MSI enabled Linux VM which uses MSI credentials to manage Azure resource
         *  - Retrieve the Virtual machine created from the MSI enabled Linux VM.
         */
        public static async Task RunSample(ArmClient client)
        {
            var rgName1 = "ComputeRG0000";
            var rgName2 = Utilities.CreateRandomName("uamsi-rg-2");
            var identityName = Utilities.CreateRandomName("id");
            var linuxVMName = Utilities.CreateRandomName("VM1");
            var pipName = Utilities.CreateRandomName("pip1");
            var userName = Utilities.CreateUsername();
            var password = Utilities.CreatePassword();

            try
            {
                //============================================================================================
                // Create a Resource Group and User Assigned MSI with CONTRIBUTOR access to the resource group

                var invokeScriptCommand = "bash install_dotnet_git.sh";
                List<string> fileUris = new List<string>()
                {
                    "https://raw.githubusercontent.com/Azure/azure-libraries-for-net/master/Samples/Asset/install_dotnet_git.sh"
                };

                Utilities.Log("Creating a Resource Group and User Assigned MSI with CONTRIBUTOR access to the resource group");

                var resourceGroup1 = azure.ResourceGroups
                        .Define(rgName1)
                        .WithRegion(region)
                        .Create();

                var identity = azure.Identities
                        .Define(identityName)
                        .WithRegion(region)
                        .WithNewResourceGroup(rgName2)
                        .WithAccessTo(resourceGroup1.Id, BuiltInRole.Contributor)
                        .Create();

                Utilities.Log("Created Resource Group and User Assigned MSI");

                Utilities.PrintResourceGroup(resourceGroup1);
                //StringBuilder info = new StringBuilder();
                //info.Append("Identity: ").Append(resource.Id)
                //.Append("\n\tName: ").Append(resource.Name)
                //        .Append("\n\tRegion: ").Append(resource.Region)
                //        .Append("\n\tTags: ").Append(resource.Tags.ToString())
                //        .Append("\n\tService Principal Id: ").Append(resource.PrincipalId)
                //        .Append("\n\tClient Id: ").Append(resource.ClientId)
                //        .Append("\n\tTenant Id: ").Append(resource.TenantId)
                //        .Append("\n\tClient Secret Url: ").Append(resource.ClientSecretUrl);

                //============================================================================================
                // Create a Linux VM and associate it with User Assigned MSI
                // Install DontNet and Git on the VM using Azure Custom Script Extension

                // The script to install DontNet and Git on a virtual machine using Azure Custom Script Extension
                //


                Utilities.Log("Creating a Linux VM with MSI associated and install DotNet and Git");

                var virtualMachine = azure.VirtualMachines
                        .Define(linuxVMName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(rgName2)
                        .WithNewPrimaryNetwork("10.0.0.0/28")
                        .WithPrimaryPrivateIPAddressDynamic()
                        .WithNewPrimaryPublicIPAddress(pipName)
                        .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
                        .WithRootUsername(userName)
                        .WithRootPassword(password)
                        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
                        .WithExistingUserAssignedManagedServiceIdentity(identity)
                        .DefineNewExtension("CustomScriptForLinux")
                            .WithPublisher("Microsoft.OSTCExtensions")
                            .WithType("CustomScriptForLinux")
                            .WithVersion("1.4")
                            .WithMinorVersionAutoUpgrade()
                            .WithPublicSetting("fileUris", fileUris)
                            .WithPublicSetting("commandToExecute", invokeScriptCommand)
                            .Attach()
                        .Create();

                Utilities.Log("Created Linux VM");

                Utilities.PrintVirtualMachine(virtualMachine);

                //=============================================================
                // Run Java application in the MSI enabled Linux VM which uses MSI credentials to manage Azure resource

                Utilities.Log("Running .NET application in the MSI enabled VM which creates another virtual machine");

                List<String> commands = new List<string>
                {
                    "git clone https://github.com/Azure-Samples/compute-dotnet-manage-vm-from-vm-with-msi-credentials.git",
                    "cd compute-dotnet-manage-vm-from-vm-with-msi-credentials",
                    $"dotnet run {azure.SubscriptionId} {rgName1} {identity.ClientId}"
                };

                RunCommandOnVM(azure, virtualMachine, commands);

                Utilities.Log("DotNet application executed");

                //=============================================================
                // Retrieve the Virtual machine created from the MSI enabled Linux VM

                Utilities.Log("Retrieving the virtual machine created from the MSI enabled Linux VM");

                var virtualMachines = azure.VirtualMachines.ListByResourceGroup(resourceGroup1.Name);
                foreach (var vm in virtualMachines)
                {
                    Utilities.PrintVirtualMachine(vm);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group: {_resourceGroupId}");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId}");
                    }
                    Utilities.Log($"Deleting Resource Group: {rgName1}");
                    azure.ResourceGroups.DeleteByName(rgName1);
                    Utilities.Log($"Deleted Resource Group: {rgName1}");

                    Utilities.Log($"Deleting Resource Group: {rgName2}");
                    azure.ResourceGroups.DeleteByName(rgName2);
                    Utilities.Log($"Deleted Resource Group: {rgName2}");
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        private static RunCommandResultInner RunCommandOnVM(IAzure azure, IVirtualMachine virtualMachine, List<String> commands)
        {
            RunCommandInput runParams = new RunCommandInput()
            {
                CommandId = "RunShellScript",
                Script = commands
            };
            return azure.VirtualMachines
                    .RunCommandAsync(virtualMachine.ResourceGroupName, virtualMachine.Name, runParams).Result;
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                await RunSample(client);
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}
