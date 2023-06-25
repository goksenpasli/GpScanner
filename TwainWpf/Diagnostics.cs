using System;
using TwainWpf.TwainNative;

namespace TwainWpf
{
    public class Diagnostics
    {
        public Diagnostics(IWindowsMessageHook messageHook)
        {
            using (DataSourceManager dataSourceManager = new DataSourceManager(DataSourceManager.DefaultApplicationId, messageHook))
            {
                dataSourceManager.SelectSource();

                DataSource dataSource = dataSourceManager.DataSource;
                dataSource.OpenSource();

                foreach (Capabilities capability in Enum.GetValues(typeof(Capabilities)))
                {
                    try
                    {
                        bool result = Capability.GetBoolCapability(capability, dataSourceManager.ApplicationId, dataSource.SourceId);

                        Console.WriteLine($"{capability}: {result}");
                    }
                    catch (TwainException e)
                    {
                        Console.WriteLine($"{capability}: {e.Message} {e.ReturnCode} {e.ConditionCode}");
                    }
                }
            }
        }
    }
}