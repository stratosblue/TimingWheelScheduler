# TimingWheelScheduler

a scheduler based on multi-level timing wheels. 一个基于多级时间轮的调度器。

## 如何使用

### 引用包

```xml
<ItemGroup>
  <PackageReference Include="TimingWheelScheduler" Version="*-*" />
</ItemGroup>
```

### 创建 `scheduler`
```C#
TimeSpan TickSpan;  //每次tick的时间长度
int TicksPerWheel;  //每层时间轮的tick数量（每层时间轮的大小）
int NumberOfWheels; //时间轮的总层数
var options = new TimingWheelSchedulerOptions(TickSpan, TicksPerWheel, NumberOfWheels)
{
    TaskScheduler = null; //TimingWheelScheduler 内部创建tick任务时所使用的 TaskScheduler，不指定时使用 TaskScheduler.Default
};

//创建manager，manager用于管理scheduler
var schedulerManager = TimingWheelScheduler.Create(options);
//获取调度器
var scheduler = schedulerManager.Scheduler;

//销毁调度器
//schedulerManager.Dispose();
```

### 使用默认的共享 `scheduler`
```C#
//TickSpan = 50ms
//TicksPerWheel = 255
//NumberOfWheels = 4 
var scheduler = TimingWheelScheduler.Shared;
```

### 创建定时任务
```
var delay = TimeSpan.FromSeconds(1);    //延时1s执行
var callback = () => Console.WriteLine("callbak");  //执行时的回调
var options = new ScheduleOptions() //调度选项，可选
{
    TaskScheduler = null,   //执行callback时使用的 TaskScheduler，不指定时使用 TaskScheduler.Default
};

//计划调度
var disposable = scheduler.Schedule(delay, callback, options);

//取消执行
//disposable.Dispose();
```

### Async Delay
```C#
using var cts = new CancellationTokenSource();
var cancellationToken = cts.Token;  //取消token，可选
var delay = TimeSpan.FromSeconds(1);    //延时1s

//延时
await scheduler.Delay(delay, cancellationToken);
```
