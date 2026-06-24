using ScreenApp.Donations;
using ScreenApp.Effects;
using ScreenApp.Overlay;
using Xunit;

namespace ScreenApp.Tests;

/// <summary>
/// Тесты очереди оверлея: подписи показываются по одной, без наложения. Используется
/// синхронный UI-инвокер и заглушка окна, позволяющая вручную завершать «анимацию».
/// </summary>
public class DonationOverlayManagerTests
{
    private sealed class SyncUiInvoker : IUiInvoker
    {
        public void Invoke(Action action) => action();
    }

    private sealed class FakeOverlayView : IOverlayView
    {
        private Action? _pending;

        public List<string> Shown { get; } = new();
        public bool Closed { get; private set; }
        public bool IsBusy => _pending is not null;

        public void ShowMessage(string text, double holdSeconds, Action onFinished)
        {
            Shown.Add(text);
            _pending = onFinished;
        }

        public void Close() => Closed = true;

        public void Finish()
        {
            var cb = _pending;
            _pending = null;
            cb?.Invoke();
        }
    }

    private static Donation Donation(string viewer, decimal amount) =>
        new() { Username = viewer, Amount = amount };

    [Fact]
    public void Messages_ShownOneAtATime_NoOverlap()
    {
        var view = new FakeOverlayView();
        var manager = new DonationOverlayManager(new SyncUiInvoker(), () => view, holdSeconds: 1.0);

        manager.ShowDonation(Donation("a", 100), "Перевернуть экран");
        manager.ShowDonation(Donation("b", 50), "Затемнение");
        manager.ShowDonation(Donation("c", 200), "Белый экран");

        Assert.Single(view.Shown);
        Assert.Equal("a — 100 ₽ — Перевернуть экран", view.Shown[0]);
        Assert.Equal(2, manager.QueuedCount);

        view.Finish();
        Assert.Equal(2, view.Shown.Count);
        Assert.Equal("b — 50 ₽ — Затемнение", view.Shown[1]);

        view.Finish();
        Assert.Equal(3, view.Shown.Count);
        Assert.Equal("c — 200 ₽ — Белый экран", view.Shown[2]);

        view.Finish();
        Assert.False(view.IsBusy);
    }

    [Fact]
    public void Dispose_ClearsQueueAndClosesView()
    {
        var view = new FakeOverlayView();
        var manager = new DonationOverlayManager(new SyncUiInvoker(), () => view, holdSeconds: 1.0);

        manager.ShowDonation(Donation("a", 100), "Перевернуть экран");
        manager.ShowDonation(Donation("b", 50), "Затемнение");

        manager.Dispose();

        Assert.Equal(0, manager.QueuedCount);
        Assert.True(view.Closed);
    }

    [Fact]
    public void Enqueue_AfterDispose_Ignored()
    {
        var view = new FakeOverlayView();
        var manager = new DonationOverlayManager(new SyncUiInvoker(), () => view, holdSeconds: 1.0);
        manager.Dispose();

        manager.Enqueue("x", "10 ₽", "Перевернуть экран");

        Assert.Equal(0, manager.QueuedCount);
    }
}
