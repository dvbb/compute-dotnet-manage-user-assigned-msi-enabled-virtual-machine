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
using Azure.ResourceManager.Authorization;
using static System.Formats.Asn1.AsnWriter;
using Azure.ResourceManager.Authorization.Models;

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

                // Create a UserAssigned Identity in resourceGroup2
                UserAssignedIdentityData userAssignedIdentityInput = new UserAssignedIdentityData(resourceGroup2.Data.Location);
                var identityLro = await resourceGroup2.GetUserAssignedIdentities().CreateOrUpdateAsync(WaitUntil.Completed, userAssignedIdentityName, userAssignedIdentityInput);
                UserAssignedIdentityResource identity = identityLro.Value;
                Utilities.Log($"Created User Assigned MSI:");
                Utilities.Log("\tName: " + identity.Data.Name);
                Utilities.Log("\tPrincipalId: " + identity.Data.PrincipalId);
                Utilities.Log("\tClientId: " + identity.Data.ClientId);
                Utilities.Log("\tTenantId: " + identity.Data.TenantId);

                // Add a contributor role to resourceGroup1 for above UserAssignedIdentity
                // `b24988ac-6180-42a0-ab88-20f7382dd24c` is contributor role definition name
                ResourceIdentifier scopeId = resourceGroup1.Id;
                string roleAssignmentName = Guid.NewGuid().ToString();
                RoleAssignmentCreateOrUpdateContent content = new RoleAssignmentCreateOrUpdateContent(
                    roleDefinitionId: new ResourceIdentifier($"{subscription.Id}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c"),
                    principalId: identity.Data.PrincipalId.Value)
                {
                    PrincipalType = RoleManagementPrincipalType.ServicePrincipal
                };
                var roleAssignmentLro = await client.GetRoleAssignments(scopeId).CreateOrUpdateAsync(WaitUntil.Completed, roleAssignmentName, content);
                RoleAssignmentResource roleAssignment = roleAssignmentLro.Value;
                Utilities.Log("Created contribute role in resourceGroup1:");
                Utilities.Log("\tName: " + roleAssignment.Data.Name);
                Utilities.Log("\tPrincipalType: " + roleAssignment.Data.PrincipalType);
                Utilities.Log("\tScope: " + roleAssignment.Data.Scope);
                Utilities.Log("\tPrincipalId: " + roleAssignment.Data.PrincipalId);

                //============================================================================================
                // Create a Linux VM and associate it with User Assigned MSI
                // Install DontNet and Git on the VM using Azure Custom Script Extension

                Utilities.Log("Creating a User Assigned MSI with CONTRIBUTOR access to the resource group");

                // The script to install DontNet and Git on a virtual machine using Azure Custom Script Extension

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
                linuxVMInput.Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.SystemAssignedUserAssigned);
                linuxVMInput.Identity.UserAssignedIdentities.Add(identity.Id, new UserAssignedIdentity());

                var linuxVmLro = await resourceGroup2.GetVirtualMachines().CreateOrUpdateAsync(WaitUntil.Completed, linuxVMName, linuxVMInput);
                VirtualMachineResource linuxVM = linuxVmLro.Value;

                Utilities.Log($"Created Linux VM: {linuxVM.Data.Name}");

                // Use a VM extension to install Apache Web servers
                // Definate vm extension input data
                var invokeScriptCommand = "bash install_dotnet_git.sh";
                List<string> fileUris = new List<string>()
                {
                    "https://raw.githubusercontent.com/dvbb/compute-dotnet-manage-user-assigned-msi-enabled-virtual-machine/master/Asset/install_dotnet_git.sh"
                };
                var extensionInput = new VirtualMachineExtensionData(resourceGroup2.Data.Location)
                {
                    Publisher = "Microsoft.OSTCExtensions",
                    ExtensionType = "CustomScriptForLinux",
                    TypeHandlerVersion = "1.4",
                    AutoUpgradeMinorVersion = true,
                    EnableAutomaticUpgrade = false,
                    Settings = BinaryData.FromObjectAsJson(new
                    {
                        fileUris = fileUris,
                        commandToExecute = invokeScriptCommand,
                    })
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
                    Utilities.Log("vm name:" + vm.Data.Name);
                    Utilities.Log("UserAssignedIdentities count:" + vm.Data.Identity.UserAssignedIdentities.Count);
                    Utilities.Log("UserAssignedIdentities:");
                    foreach (var item in vm.Data.Identity.UserAssignedIdentities)
                    {
                        Utilities.Log("\t" + item.Key);
                    }
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
