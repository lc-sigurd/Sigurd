using Sigurd.Bus.Api;

namespace Sigurd.Common;

public class SigurdLib
{
    public static readonly IEventBus EventBus = new BusConfiguration {
        StartImmediately = false,
    }.Build();
}
