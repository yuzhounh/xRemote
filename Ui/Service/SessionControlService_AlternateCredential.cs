using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;
using Stylet;
using _1RM.View.Utils;
using _1RM.Model;

namespace _1RM.Service
{
    public partial class SessionControlService
    {
#if DEBUG
        public static void CredentialTest()
        {
            var pingCredentials = new List<Credential>
            {
                new Credential()
                {
                    Name = "asfasdas12312312312",
                    Address = "127.012311.131231231", Port = "5000",
                },
                new Credential()
                {
                    Name = "asfasdas",
                    Address = "127.0.1.1", Port = "5000",
                },
                new Credential()
                {
                    Address = "127.0.0.1", Port = "5000",
                },
                new Credential()
                {
                    Name = "xcv1",
                    Address = "192.168.100.1", Port = "3389",
                },
                new Credential()
                {
                    Name = "asfasdxxxxxxxxxxxxxxxxas12312312312",
                    Address = "172.20.65.31", Port = "3389",
                },
                new Credential()
                {
                    Name = "98vs",
                    Address = "172.20.65.64", Port = "3389",
                },
                new Credential()
                {
                    Name = "ggg232",
                    Address = "172.20.65.65", Port = "3389",
                },
                new Credential()
                {
                    Name = "zxd11",
                    Address = "172.20.65.66", Port = "3389",
                },
            };
            Task.Factory.StartNew(async () =>
            {
                var c = await FindFirstConnectableAddressAsync(pingCredentials, "test");
                if (c != null)
                {
                    SimpleLogHelper.Debug($"Connected to {c.Address}:{c.Port}");
                }
            });
        }
#endif

        /// <summary>
        /// Find the first connectable address from the given credentials. if return null then no address is connectable.
        /// </summary>
        public static async Task<Credential?> FindFirstConnectableAddressAsync(IEnumerable<Credential> pingCredentials, string protocolDisplayName)
        {
            var credentials = pingCredentials.Select(x => x.CloneMe()).ToList();
            if (credentials.Count == 0) return null;
            const int maxWaitSeconds = 5;
            using var cts = new CancellationTokenSource();
            var items = new List<PingTestItem>();
            foreach (var credential in credentials)
            {
                credential.Address = string.IsNullOrEmpty(credential.Address) ? credentials[0].Address : credential.Address;
                credential.Port = string.IsNullOrEmpty(credential.Port) ? credentials[0].Port : credential.Port;
                items.Add(new PingTestItem(credential.Name, credential.Address + ":" + credential.Port));
            }
            var dlg = new AlternateAddressSwitchingViewModel(cts)
            {
                Title = protocolDisplayName + " · " + UiText.Get("ConnectionCheck.Title"),
                PingTestItems = items
            };
            await Execute.OnUIThreadAsync(() =>
            {
                dlg.BeginAttempt(maxWaitSeconds);
                IoC.Get<IWindowManager>().ShowWindow(dlg);
            });

            while (!cts.IsCancellationRequested)
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                await Execute.OnUIThreadAsync(() => dlg.BeginAttempt(maxWaitSeconds));

                async Task<bool?> ProbeAsync(int index)
                {
                    var credential = credentials[index];
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    bool? result;
                    try
                    {
                        result = await TcpHelper.TestConnectionAsync(credential.Address, credential.Port,
                            attempt.Token, maxWaitSeconds * 1000).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { result = null; }
                    catch (Exception ex)
                    {
                        SimpleLogHelper.Error(ex);
                        result = false;
                    }
                    await Execute.OnUIThreadAsync(() =>
                    {
                        if (dlg.IsCanceled) return;
                        items[index].Ping = (int)watch.ElapsedMilliseconds;
                        items[index].Status = result == true ? PingStatus.Success
                            : attempt.IsCancellationRequested ? PingStatus.Canceled
                            : result == null ? PingStatus.TimedOut : PingStatus.Failed;
                    });
                    return result;
                }

                async Task CountDownAsync()
                {
                    try
                    {
                        for (var remaining = maxWaitSeconds - 1; remaining >= 0; remaining--)
                        {
                            await Task.Delay(1000, attempt.Token).ConfigureAwait(false);
                            await Execute.OnUIThreadAsync(() => { if (!dlg.IsCanceled) dlg.Eta = remaining; });
                        }
                    }
                    catch (OperationCanceledException) { }
                }

                var countdown = CountDownAsync();
                var probes = credentials.Select((_, index) => ProbeAsync(index)).ToList();
                var remainingProbes = probes.ToList();
                Credential? successfulCredential = null;
                try
                {
                    while (remainingProbes.Count > 0)
                    {
                        var completed = await Task.WhenAny(remainingProbes).ConfigureAwait(false);
                        remainingProbes.Remove(completed);
                        if (await completed.ConfigureAwait(false) == true)
                        {
                            successfulCredential = credentials[probes.IndexOf(completed)];
                            break;
                        }
                    }
                }
                finally
                {
                    attempt.Cancel();
                    await Task.WhenAll(probes).ConfigureAwait(false);
                    await countdown.ConfigureAwait(false);
                }

                if (cts.IsCancellationRequested) return null;
                if (successfulCredential != null)
                {
                    await Execute.OnUIThreadAsync(() => dlg.RequestClose());
                    return successfulCredential.CloneMe();
                }

                Task<bool>? retry = null;
                await Execute.OnUIThreadAsync(() =>
                {
                    if (!dlg.IsCanceled) retry = dlg.ShowFailure(items.Any(item => item.Status == PingStatus.TimedOut));
                });
                if (retry == null || !await retry.ConfigureAwait(false)) return null;
            }
            return null;
        }


        /// <summary>
        /// Find the first connectable address from the given credentials. if return null then no address is connectable.
        /// </summary>
        public static async Task<Credential?> FindFirstConnectableAddressAsyncWithoutUI(IEnumerable<Credential> pingCredentials, string protocolDisplayName)
        {
            const int maxWaitSeconds = 5;
            var credentials = pingCredentials.Select(x => x.CloneMe()).ToList();
            if (!credentials.Any())
            {
                return null;
            }

            using var cts = new CancellationTokenSource();

            // Normalize credentials - ensure all have address and port
            foreach (var credential in credentials)
            {
                credential.Address = string.IsNullOrEmpty(credential.Address) ? credentials.First().Address : credential.Address;
                credential.Port = string.IsNullOrEmpty(credential.Port) ? credentials.First().Port : credential.Port;
            }

            SimpleLogHelper.Debug($"FindFirstConnectableAddressSimpleAsync testing {credentials.Count} addresses...");

            var taskList = new List<(Task<bool?> Task, Credential Credential)>();
            for (int i = 0; i < credentials.Count; i++)
            {
                // add tasks to ping each credential
                var credential = credentials[i];
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var ret = await TcpHelper.TestConnectionAsync(credential.Address, credential.Port, cts.Token, maxWaitSeconds * 1000);
                        return ret;
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                    catch (Exception ex)
                    {
                        SimpleLogHelper.Debug($"Error testing connection: {ex.Message}");
                        return false;
                    }
                }, cts.Token);
                taskList.Add((task, credential));
            }

            Credential? successfulCredential = null;
            try
            {
                var remainingTasks = taskList.Select(t => t.Task).ToList();
                while (remainingTasks.Any())
                {
                    var completedTask = await Task.WhenAny(remainingTasks);
                    remainingTasks.Remove(completedTask);

                    // if success then get credential
                    var taskInfo = taskList.FirstOrDefault(t => t.Task == completedTask);
                    if (taskInfo != default && await taskInfo.Task == true)
                    {
                        successfulCredential = taskInfo.Credential;
                        SimpleLogHelper.DebugInfo($"Task for credential {successfulCredential.Name} completed successfully. Cancelling remaining tasks.");
                        break;
                    }
                }
            }
            finally
            {
                // Cancel all remaining tasks
                cts.Cancel();
            }

            // Return the first credential when ping success
            if (successfulCredential != null)
            {
                return successfulCredential.CloneMe();
            }

            // None of the addresses are connectable
            SimpleLogHelper.Debug("No connectable address found");
            return null;
        }


        /// <summary>
        /// if return null then no address is connectable.
        /// </summary>
        public static async Task<Credential?> GetCredential(ProtocolBaseWithAddressPort protocol, string assignCredentialName = "")
        {
            // set the credential from the raw protocol (for reconnection since the credential may be changed when first connection)
            if (IoC.Get<GlobalData>().VmItemList.FirstOrDefault(x => x.Id == protocol.Id) is { Server: ProtocolBaseWithAddressPort swap })
            {
                var swap2 = (ProtocolBaseWithAddressPort)swap.Clone();
                swap2.DecryptToConnectLevel();
                protocol.DisplayName = swap2.DisplayName;
                protocol.SetCredential(swap2.GetCredential(), true);
            }

            var newCredential = protocol.GetCredential();
            newCredential.Name = protocol.DisplayName;
            // use assign credential 应用指定的 credential
            var assignCredential = protocol.AlternateCredentials.FirstOrDefault(x => x.Name == assignCredentialName);
            if (assignCredential != null)
            {
                SimpleLogHelper.Debug("using assign credential: " + assignCredentialName);
                newCredential.SetCredential(assignCredential);
                if (protocol.DisplayName != assignCredentialName)
                    newCredential.Name = $"{protocol.DisplayName} ({assignCredentialName})";
            }

            // check if it needs to ping before connect
            bool isPingBeforeConnect = protocol.IsPingBeforeConnect == true
                                       // do not ping if rdp protocol and gateway is used
                                       && protocol is not RDP { GatewayMode: EGatewayMode.UseTheseGatewayServerSettings };
            // check if it needs to auto switch address
            var isAutoAlternateAddressSwitching = protocol.IsAutoAlternateAddressSwitching == true
                                                  // if any host or port in assignCredential，then disabled `AutoAlternateAddressSwitching`
                                                  && string.IsNullOrEmpty(assignCredential?.Address) && string.IsNullOrEmpty(assignCredential?.Port)
                                                  // if none of the alternate credential has host or port，then disabled `AutoAlternateAddressSwitching`
                                                  && protocol.AlternateCredentials.Any(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port));

            if (
                // if both `IsPingBeforeConnect` and `IsAutoAlternateAddressSwitching` are false, then return directly
                (isPingBeforeConnect == false && isAutoAlternateAddressSwitching == false)
                // if LocalApp protocol are not showing address input, then return directly
                || (protocol is LocalApp app && app.ShowAddressInput() == false))
            {
                return newCredential;
            }

            // a quick test for the first credential, if pass return directly to avoid window pop
            var ret = await TcpHelper.TestConnectionAsync(newCredential.Address, newCredential.Port, null, 100);
            if (ret == true)
                return newCredential;

            var credentials = new List<Credential> { newCredential };
            // if `IsAutoAlternateAddressSwitching` is true, then add all alternate credentials, else only add the main address to ping
            if (isAutoAlternateAddressSwitching)
                credentials.AddRange(protocol.AlternateCredentials.Where(x => !string.IsNullOrEmpty(x.Address) || !string.IsNullOrEmpty(x.Port)));

            // find the first response address from `credentials`
            var connectableAddress = await FindFirstConnectableAddressAsync(credentials, protocol.DisplayName);
            if (connectableAddress != null)
            {
                newCredential = connectableAddress.CloneMe();
                if (protocol.DisplayName != connectableAddress.Name)
                    newCredential.Name = $"{protocol.DisplayName} ({connectableAddress.Name})";
                return newCredential;
            }
            return null;
        }
    }
}
