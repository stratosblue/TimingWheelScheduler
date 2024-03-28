using static System.Threading.Tasks.ScheduledTask;

namespace TimingWheelSchedulerTest;

[TestClass]
public class ScheduledTaskCallbackNodeTest
{
    #region Public 方法

    [TestMethod]
    public void ShouldParallelAppendSuccess()
    {
        const int TotalCount = 50_000;

        var count = 0;
        var rootNode = new ScheduledTaskCallbackNode(Increase);

        Parallel.For(0, TotalCount, _ =>
        {
            rootNode.AppendForExecute(new(Increase));
        });

        rootNode.Execute();

        Assert.AreEqual(TotalCount + 1, count);

        void Increase()
        {
            Interlocked.Increment(ref count);
        }
    }

    [TestMethod]
    public void ShouldParallelAppendWithExecuteExceptionSuccess()
    {
        const int TotalCount = 50_000;

        var count = 0;
        var rootNode = new ScheduledTaskCallbackNode(Increase);
        var exceptionCount = 0;

        Parallel.For(0, TotalCount, index =>
        {
            if (index % 1000 == 0)
            {
                try
                {
                    rootNode.AppendForExecute(new(() =>
                    {
                        Interlocked.Increment(ref exceptionCount);
                        throw new TestException();
                    }));
                }
                catch (AggregateException ex)
                {
                    CollectionAssert.AllItemsAreInstancesOfType(ex.InnerExceptions, typeof(TestException));
                }
            }
            else
            {
                rootNode.AppendForExecute(new(Increase));
            }

            if (index == TotalCount / 2)
            {
                try
                {
                    rootNode.Execute();
                }
                catch (AggregateException ex)
                {
                    CollectionAssert.AllItemsAreInstancesOfType(ex.InnerExceptions, typeof(TestException));
                }
            }
        });

        Assert.AreEqual(TotalCount + 1, count + exceptionCount);

        void Increase()
        {
            Interlocked.Increment(ref count);
        }
    }

    [TestMethod]
    public void ShouldParallelAppendWithExecuteSuccess()
    {
        const int TotalCount = 50_000;

        var count = 0;
        var rootNode = new ScheduledTaskCallbackNode(Increase);

        Parallel.For(0, TotalCount, index =>
        {
            rootNode.AppendForExecute(new(Increase));
            if (index == TotalCount / 2)
            {
                rootNode.Execute();
            }
        });

        Assert.AreEqual(TotalCount + 1, count);

        void Increase()
        {
            Interlocked.Increment(ref count);
        }
    }

    #endregion Public 方法

    #region Public 类

    public sealed class TestException : Exception
    {
        #region Public 构造函数

        public TestException() : base("Exception test")
        {
        }

        #endregion Public 构造函数
    }

    #endregion Public 类
}
