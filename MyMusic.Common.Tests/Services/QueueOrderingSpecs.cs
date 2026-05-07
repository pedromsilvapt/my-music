using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class QueueOrderingSpecs
{
    #region Gap Calculation Tests

    [Fact]
    public void NeedsRebalance_WithSmallGap_ReturnsTrue()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 1000.0005, 2000.0 };
        
        // Act
        var result = NeedsRebalance(orders);
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void NeedsRebalance_WithAdequateGap_ReturnsFalse()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 2000.0, 3000.0 };
        
        // Act
        var result = NeedsRebalance(orders);
        
        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void NeedsRebalance_WithSingleElement_ReturnsFalse()
    {
        // Arrange
        var orders = new List<double> { 1000.0 };
        
        // Act
        var result = NeedsRebalance(orders);
        
        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void NeedsRebalance_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var orders = new List<double>();
        
        // Act
        var result = NeedsRebalance(orders);
        
        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void RebalanceOrders_CreatesSequentialGaps()
    {
        // Act
        var result = RebalanceOrders(5);
        
        // Assert
        result.ShouldBe([1000.0, 2000.0, 3000.0, 4000.0, 5000.0]);
    }

    #endregion

    #region Play Next Gap Tests

    [Fact]
    public void PlayNext_WithEmptyQueue_UsesFirstGap()
    {
        // Arrange
        var currentSongOrder = (double?)null;
        var allOrders = new List<double>();
        
        // Act
        var insertOrder = CalculatePlayNextOrder(currentSongOrder, allOrders);
        
        // Assert
        insertOrder.ShouldBe(1000.0);
    }

    [Fact]
    public void PlayNext_AtEndOfQueue_UsesGapAfterLast()
    {
        // Arrange
        var currentSongOrder = 2000.0;
        var allOrders = new List<double> { 1000.0, 2000.0 };
        
        // Act
        var insertOrder = CalculatePlayNextOrder(currentSongOrder, allOrders);
        
        // Assert
        insertOrder.ShouldBe(3000.0);
    }

    [Fact]
    public void PlayNext_InMiddleOfQueue_UsesMidpoint()
    {
        // Arrange
        var currentSongOrder = 1000.0;
        var allOrders = new List<double> { 1000.0, 2000.0, 3000.0 };
        
        // Act
        var insertOrder = CalculatePlayNextOrder(currentSongOrder, allOrders);
        
        // Assert
        insertOrder.ShouldBe(1500.0);
    }

    [Fact]
    public void PlayNext_BetweenTwoSongs_CalculatesCorrectMidpoint()
    {
        // Arrange
        var currentSongOrder = 5000.0;
        var allOrders = new List<double> { 1000.0, 3000.0, 5000.0, 7000.0, 9000.0 };
        
        // Act
        var insertOrder = CalculatePlayNextOrder(currentSongOrder, allOrders);
        
        // Assert
        insertOrder.ShouldBe(6000.0);
    }

    #endregion

    #region Play Last Gap Tests

    [Fact]
    public void PlayLast_WithEmptyQueue_UsesFirstGap()
    {
        // Arrange
        var maxOrder = 0.0;
        
        // Act
        var insertOrder = CalculatePlayLastOrder(maxOrder);
        
        // Assert
        insertOrder.ShouldBe(1000.0);
    }

    [Fact]
    public void PlayLast_WithSongsInQueue_UsesGapAfterMax()
    {
        // Arrange
        var maxOrder = 5000.0;
        
        // Act
        var insertOrder = CalculatePlayLastOrder(maxOrder);
        
        // Assert
        insertOrder.ShouldBe(6000.0);
    }

    #endregion

    #region Reorder Tests

    [Fact]
    public void Reorder_MoveForward_UsesMidpoint()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 2000.0, 3000.0, 4000.0, 5000.0 };
        var fromIndex = 0;
        var toIndex = 2;
        
        // Act
        var newOrder = CalculateReorderPosition(fromIndex, toIndex, orders);
        
        // Assert
        newOrder.ShouldBe(3500.0);
    }

    [Fact]
    public void Reorder_MoveBackward_UsesMidpoint()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 2000.0, 3000.0, 4000.0, 5000.0 };
        var fromIndex = 4;
        var toIndex = 1;
        
        // Act
        var newOrder = CalculateReorderPosition(fromIndex, toIndex, orders);
        
        // Assert
        newOrder.ShouldBe(1500.0);
    }

    [Fact]
    public void Reorder_MoveToStart_UsesGapBeforeFirst()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 2000.0, 3000.0 };
        var fromIndex = 2;
        var toIndex = 0;
        
        // Act
        var newOrder = CalculateReorderPosition(fromIndex, toIndex, orders);
        
        // Assert
        newOrder.ShouldBe(0.0);
    }

    [Fact]
    public void Reorder_MoveToEnd_UsesGapAfterLast()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 2000.0, 3000.0 };
        var fromIndex = 0;
        var toIndex = 2;
        
        // Act
        var newOrder = CalculateReorderPosition(fromIndex, toIndex, orders);
        
        // Assert
        newOrder.ShouldBe(4000.0);
    }

    #endregion

    #region Multiple Insertions Tests

    [Fact]
    public void ConsecutivePlayNextInsertions_MaintainOrdering()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 2000.0 };
        var currentSongOrder = 1000.0;
        
        // Act
        var firstInsert = CalculatePlayNextOrder(currentSongOrder, orders);
        orders.Add(firstInsert);
        orders.Sort();
        
        var secondInsert = CalculatePlayNextOrder(firstInsert, orders);
        orders.Add(secondInsert);
        orders.Sort();
        
        // Assert
        orders.ShouldBe([1000.0, 1500.0, 1750.0, 2000.0]);
    }

    [Fact]
    public void ManyConsecutiveInsertions_TriggersNeedForRebalance()
    {
        // Arrange
        var orders = new List<double> { 1000.0 };
        var currentOrder = 1000.0;
        
        // Act
        for (int i = 0; i < 20; i++)
        {
            var insertOrder = currentOrder + 0.001;
            orders.Add(insertOrder);
            orders.Sort();
            currentOrder = insertOrder;
        }
        
        var needsRebalance = NeedsRebalance(orders);
        
        // Assert
        needsRebalance.ShouldBeTrue();
    }

    [Fact]
    public void RebalanceAfterManyInsertions_RestoresStandardGaps()
    {
        // Arrange
        var orders = new List<double> { 1000.0 };
        for (int i = 0; i < 5; i++)
        {
            orders.Add(1000.0 + (i + 1) * 0.001);
        }
        orders.Sort();
        
        // Act
        var rebalancedOrders = RebalanceOrders(orders.Count);
        
        // Assert
        rebalancedOrders.ShouldBe([1000.0, 2000.0, 3000.0, 4000.0, 5000.0, 6000.0]);
    }

    #endregion

    #region Display Order Tests

    [Fact]
    public void DisplayOrder_FromInternalOrder_IsSequentialOneIndexed()
    {
        // Arrange
        var internalOrders = new List<double> { 1000.0, 1500.0, 3000.0, 50000.0 };
        var sortedSongs = internalOrders.Order().ToList();
        
        // Act
        var displayOrders = sortedSongs.Select((_, index) => index + 1).ToList();
        
        // Assert
        displayOrders.ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void DisplayOrder_AfterGapInsertion_MaintainsCorrectSequence()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 1500.0, 2000.0 };
        
        // Act
        var displayOrders = orders.Order().Select((_, index) => index + 1).ToList();
        
        // Assert
        displayOrders.ShouldBe([1, 2, 3]);
    }

    #endregion

    #region Remove Tests (No Reorder Needed)

    [Fact]
    public void RemoveFromQueue_DoesNotChangeOtherOrders()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 2000.0, 3000.0 };
        
        // Act
        orders.Remove(2000.0);
        
        // Assert
        orders.ShouldBe([1000.0, 3000.0]);
    }

    [Fact]
    public void RemoveFromQueue_PreservesGapStructure()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 1500.0, 2000.0, 3000.0 };
        
        // Act
        orders.Remove(1500.0);
        
        // Assert
        orders.ShouldBe([1000.0, 2000.0, 3000.0]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void NeedsRebalance_WithVerySmallGap_ReturnsTrue()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 1000.0000001, 2000.0 };
        
        // Act
        var result = NeedsRebalance(orders);
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void NeedsRebalance_WithZeroGap_ReturnsTrue()
    {
        // Arrange
        var orders = new List<double> { 1000.0, 1000.0, 2000.0 };
        
        // Act
        var result = NeedsRebalance(orders);
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void RebalanceOrders_WithSingleElement_ReturnsSingleGap()
    {
        // Act
        var result = RebalanceOrders(1);
        
        // Assert
        result.ShouldBe([1000.0]);
    }

    [Fact]
    public void RebalanceOrders_WithEmptyList_ReturnsEmptyList()
    {
        // Act
        var result = RebalanceOrders(0);
        
        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region Helper Methods

    private static bool NeedsRebalance(IReadOnlyList<double> orders)
    {
        if (orders.Count < 2) return false;

        var sortedOrders = orders.Order().ToList();
        for (var i = 1; i < sortedOrders.Count; i++)
        {
            var gap = sortedOrders[i] - sortedOrders[i - 1];
            if (gap < 0.001)
            {
                return true;
            }
        }

        return false;
    }

    private static List<double> RebalanceOrders(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => (i + 1) * 1000.0)
            .ToList();
    }

    private static double CalculatePlayNextOrder(double? currentSongOrder, IReadOnlyList<double> allOrders)
    {
        if (!currentSongOrder.HasValue || allOrders.Count == 0)
        {
            return 1000.0;
        }

        var sortedOrders = allOrders.Order().ToList();
        var currentIndex = sortedOrders.ToList().BinarySearch(currentSongOrder.Value);
        if (currentIndex < 0)
        {
            currentIndex = ~currentIndex - 1;
        }

        if (currentIndex < 0 || currentIndex >= sortedOrders.Count - 1)
        {
            return currentSongOrder.Value + 1000.0;
        }

        var currentOrder = sortedOrders[currentIndex];
        var nextOrder = sortedOrders[currentIndex + 1];

        return (currentOrder + nextOrder) / 2.0;
    }

    private static double CalculatePlayLastOrder(double maxOrder)
    {
        return maxOrder + 1000.0;
    }

    private static double CalculateReorderPosition(int fromIndex, int toIndex, IReadOnlyList<double> orders)
    {
        if (toIndex == 0)
        {
            return orders[0] - 1000.0;
        }

        if (toIndex == orders.Count - 1)
        {
            return orders[^1] + 1000.0;
        }

        int lowerIndex, higherIndex;
        if (toIndex > fromIndex)
        {
            lowerIndex = Math.Min(toIndex, orders.Count - 1);
            higherIndex = Math.Min(toIndex + 1, orders.Count - 1);
        }
        else
        {
            lowerIndex = Math.Max(0, toIndex - 1);
            higherIndex = toIndex;
        }

        var prevOrder = orders[lowerIndex];
        var nextOrder = orders[higherIndex];

        return (prevOrder + nextOrder) / 2.0;
    }

    #endregion
}