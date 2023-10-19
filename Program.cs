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
using Azure.ResourceManager.Models;
using Azure.ResourceManager.ManagedServiceIdentities;

namespace ManageUserAssignedMSIEnabledVirtualMachine
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId1 = null;
        private static ResourceIdentifier? _resourceGroupId2 = null;

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
            string rgName1 = Utilities.CreateRandomName("ComputeSampleRG-1-");
            string rgName2 = Utilities.CreateRandomName("ComputeSampleRG-2-");
            string userAssignedIdentityName = Utilities.CreateRandomName("identity");
            string vnetName = Utilities.CreateRandomName("vnet");
            string nicName = Utilities.CreateRandomName("nic");
            string publicIpDnsLabel = Utilities.CreateRandomName("pip");
            string linuxVMName = Utilities.CreateRandomName("VM");

            try
            {
                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                // Create two resource groups in eastus
                Utilities.Log($"creating resource group1...");
                ArmOperation<ResourceGroupResource> rgLro1 = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName1, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup1 = rgLro1.Value;
                _resourceGroupId1 = resourceGroup1.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup1.Data.Name);

                ArmOperation<ResourceGroupResource> rgLro2 = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName2, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup2 = rgLro2.Value;
                _resourceGroupId2 = resourceGroup2.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup2.Data.Name);

                //============================================================================================
                // Create a Resource Group and User Assigned MSI with CONTRIBUTOR access to the resource group

                var invokeScriptCommand = "bash install_dotnet_git.sh";
                List<string> fileUris = new List<string>()
                {
                    "https://raw.githubusercontent.com/Azure/azure-libraries-for-net/master/Samples/Asset/install_dotnet_git.sh"
                };

                Utilities.Log("Creating a User Assigned MSI with CONTRIBUTOR access to the resource group");

                {
                    //// Create a managed identity in resourceGroup2
                    //string userAssignedIdentityName = Utilities.CreateRandomName("identity");
                    //ResourceIdentifier userIdentityId = new ResourceIdentifier($"{resourceGroup2.Id}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{userAssignedIdentityName}");

                    //var identityInput = new GenericResourceData(resourceGroup2.Data.Location);
                    //var identityLro = await client.GetGenericResources().CreateOrUpdateAsync(WaitUntil.Completed, userIdentityId, identityInput);
                }

                UserAssignedIdentityData userAssignedIdentityInput = new UserAssignedIdentityData(resourceGroup2.Data.Location);
                var identityLro = await resourceGroup2.GetUserAssignedIdentities().CreateOrUpdateAsync(WaitUntil.Completed, userAssignedIdentityName, userAssignedIdentityInput);
                UserAssignedIdentityResource identity = identityLro.Value;
                ;
                //var identity = azure.Identities
                //        .Define(identityName)
                //        .WithRegion(region)
                //        .WithNewResourceGroup(rgName2)
                //        .WithAccessTo(resourceGroup1.Id, BuiltInRole.Contributor)
                //        .Create();

                Utilities.Log($"Created User Assigned MSI: {identity.Data.Location}-{identity.Id.ResourceGroupName}-{identity.Data.Name}");

                //Utilities.PrintResourceGroup(resourceGroup1);
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


                Utilities.Log("Pre-creating some resources that the VM depends on");

                // Creating a virtual network
                var vnet = await Utilities.CreateVirtualNetwork(resourceGroup2, vnetName);

                // Creating public ip
                var pip = await Utilities.CreatePublicIP(resourceGroup2, publicIpDnsLabel);

                // Creating network interface
                var nic = await Utilities.CreateNetworkInterface(resourceGroup2, vnet.Data.Subnets[0].Id, pip.Id, nicName);

                Utilities.Log("Creating a Linux VM with MSI associated and install DotNet and Git");


                VirtualMachineData linuxVMInput = new VirtualMachineData(resourceGroup2.Data.Location)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardF2
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        ImageReference = new ImageReference()
                        {
                            Publisher = "Canonical",
                            Offer = "UbuntuServer",
                            Sku = "16.04-LTS",
                            Version = "latest",
                        },
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        AdminUsername = Utilities.CreateUsername(),
                        AdminPassword = Utilities.CreatePassword(),
                        ComputerName = linuxVMName,
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nic.Id,
                                Primary = true,
                            }
                        }
                    },
                };
                // Add UserAssignedIdentity
                linuxVMInput.Identity.UserAssignedIdentities.Add(identity.Id, new UserAssignedIdentity());

                var linuxVmLro = await resourceGroup2.GetVirtualMachines().CreateOrUpdateAsync(WaitUntil.Completed, linuxVMName, linuxVMInput);
                VirtualMachineResource linuxVM = linuxVmLro.Value;

                Utilities.Log($"Created Linux VM: {linuxVM.Data.Name}");

                // Use a VM extension to install Apache Web servers
                // Definate vm extension input data
                var extensionInput = new VirtualMachineExtensionData(resourceGroup2.Data.Location)
                {
                    Publisher = "Microsoft.OSTCExtensions",
                    ExtensionType = "CustomScriptForLinux",
                    TypeHandlerVersion = "1.4",
                    AutoUpgradeMinorVersion = true,
                    EnableAutomaticUpgrade = false,
                    Settings = BinaryData.FromObjectAsJson(new
                    {
                        fileUris = fileUris
                    }),
                    ProtectedSettings = BinaryData.FromObjectAsJson(new
                    {
                        commandToExecute = invokeScriptCommand,
                    }),
                };
                _ = await linuxVM.GetVirtualMachineExtensions().CreateOrUpdateAsync(WaitUntil.Completed, "CustomScriptForLinux", extensionInput);
                Utilities.Log($"Use a VM extension to install Apache Web servers...");


                //=============================================================
                // Run Java application in the MSI enabled Linux VM which uses MSI credentials to manage Azure resource

                Utilities.Log("Running .NET application in the MSI enabled VM which creates another virtual machine");

                List<String> commands = new List<string>
                {
                    "git clone https://github.com/Azure-Samples/compute-dotnet-manage-vm-from-vm-with-msi-credentials.git",
                    "cd compute-dotnet-manage-vm-from-vm-with-msi-credentials",
                    $"dotnet run {identity.Id.SubscriptionId} {rgName1} {identity.Data.ClientId}"
                };

                await RunCommandOnVM(linuxVM, commands);

                Utilities.Log("DotNet application executed");

                //=============================================================
                // Retrieve the Virtual machine created from the MSI enabled Linux VM

                Utilities.Log("Retrieving the virtual machine created from the MSI enabled Linux VM");

                await foreach (var vm in resourceGroup2.GetVirtualMachines().GetAllAsync())
                {
                    Utilities.Log(vm.Data.Name);//....todo
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
                    if (_resourceGroupId1 is not null)
                    {
                        Utilities.Log($"Deleting Resource Group: {_resourceGroupId1}");
                        await client.GetResourceGroupResource(_resourceGroupId1).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId1.Name}");
                    }
                    if (_resourceGroupId2 is not null)
                    {
                        Utilities.Log($"Deleting Resource Group: {_resourceGroupId2}");
                        await client.GetResourceGroupResource(_resourceGroupId2).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId2.Name}");
                    }
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

        private static async Task<VirtualMachineRunCommandResult> RunCommandOnVM(VirtualMachineResource virtualMachine, List<String> commands)
        {
            RunCommandInput runParams = new RunCommandInput("RunShellScript");
            foreach (var commoand in commands)
            {
                runParams.Script.Add(commoand);

            }

            var result = await virtualMachine.RunCommandAsync(WaitUntil.Completed, runParams);
            return result.Value;
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
