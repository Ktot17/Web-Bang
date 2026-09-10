using AutoFixture.Xunit2;
using DBComponent;
using Moq;
using DBComponent.Postgres;
using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace DBUnitTests;

public class UserRepositoryTests
{
    private readonly Mock<ServerDbContext> _mockContext = new();
    private readonly Mock<DbSet<User>> _mockUsersDbSet = new();
    private readonly UserRepository _userRepository;

    public UserRepositoryTests()
    {
        var usersData = new List<User>().AsQueryable();

        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Provider).Returns(usersData.Provider);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Expression).Returns(usersData.Expression);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.ElementType).Returns(usersData.ElementType);
        using var enumerator = usersData.GetEnumerator();
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        _mockContext.Setup(c => c.Users).Returns(_mockUsersDbSet.Object);

        _userRepository = new UserRepository(_mockContext.Object);
    }

    [Theory, AutoData]
    public void AddUser_WhenCalled_AddsUserAndSavesChanges(Guid userId,
        string name, string email, string passwordHash)
    {
        // Act
        _userRepository.AddUser(new User(userId, name, email, passwordHash, null));

        // Assert
        _mockUsersDbSet.Verify(m => m.Add(It.Is<User>(u =>
            u.Id == userId && u.Name == name && u.PasswordHash == passwordHash && u.GameId == null)),
            Times.Once);
        _mockContext.Verify(m => m.SaveChanges(), Times.Once);
    }

    [Theory, AutoData]
    public void FindUser_WhenUserExists_ReturnsUser(User user)
    {
        // Arrange
        var usersData = new List<User> { user }.AsQueryable();

        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Provider).Returns(usersData.Provider);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Expression).Returns(usersData.Expression);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.ElementType).Returns(usersData.ElementType);
        using var enumerator = usersData.GetEnumerator();
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        // Act
        var result = _userRepository.FindUser(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Name, result.Name);
        Assert.Equal(user.PasswordHash, result.PasswordHash);
        Assert.Equal(user.GameId, result.GameId);
    }

    [Theory, AutoData]
    public void FindUser_WhenUserDoesNotExist_ReturnsNull(Guid userId)
    {
        // Act
        var result = _userRepository.FindUser(userId);

        // Assert
        Assert.Null(result);
    }

    [Theory, AutoData]
    public void FindUserByName_WhenUserExists_ReturnsUser(User user)
    {
        // Arrange
        var usersData = new List<User> { user }.AsQueryable();

        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Provider).Returns(usersData.Provider);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Expression).Returns(usersData.Expression);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.ElementType).Returns(usersData.ElementType);
        using var enumerator = usersData.GetEnumerator();
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        // Act
        var result = _userRepository.FindUserByName(user.Name);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Name, result.Name);
        Assert.Equal(user.PasswordHash, result.PasswordHash);
        Assert.Equal(user.GameId, result.GameId);
    }

    [Theory, AutoData]
    public void FindUserByName_WhenUserDoesNotExist_ReturnsNull(string name)
    {
        // Act
        var result = _userRepository.FindUserByName(name);

        // Assert
        Assert.Null(result);
    }

    [Theory, AutoData]
    public void UpdateUser_WhenUserExists_UpdatesUserAndSavesChanges(User user, string newName)
    {
        // Arrange
        var usersData = new List<User> { user }.AsQueryable();

        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Provider).Returns(usersData.Provider);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Expression).Returns(usersData.Expression);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.ElementType).Returns(usersData.ElementType);
        using var enumerator = usersData.GetEnumerator();
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);
        user.Name = newName;

        // Act
        _userRepository.UpdateUser(user);

        // Assert
        _mockUsersDbSet.Verify(m => m.Update(It.Is<User>(u =>
            u.Id == user.Id &&
            u.Name == newName &&
            u.PasswordHash == user.PasswordHash &&
            u.GameId == user.GameId)), Times.Once);
        _mockContext.Verify(m => m.SaveChanges(), Times.Once);
    }

    [Theory, AutoData]
    public void UpdateUser_WhenUserDoesNotExist_ThrowsException(User user, string newName)
    {
        // Arrange
        user.Name = newName;

        // Act & Assert
        Assert.Throws<EntityNotFoundException>(() =>
            _userRepository.UpdateUser(user));
    }

    [Theory, AutoData]
    public void DeleteUser_WhenUserExists_DeletesUserAndSavesChanges(User user)
    {
        // Arrange
        var usersData = new List<User> { user }.AsQueryable();

        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Provider).Returns(usersData.Provider);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Expression).Returns(usersData.Expression);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.ElementType).Returns(usersData.ElementType);
        using var enumerator = usersData.GetEnumerator();
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        // Act
        _userRepository.DeleteUser(user);

        // Assert
        _mockUsersDbSet.Verify(m => m.Remove(It.Is<User>(u =>
            u.Id == user.Id &&
            u.Name == user.Name &&
            u.PasswordHash == user.PasswordHash &&
            u.GameId == user.GameId)), Times.Once);
        _mockContext.Verify(m => m.SaveChanges(), Times.Once);
    }

    [Theory, AutoData]
    public void DeleteUser_WhenUserDoesNotExist_ThrowsException(User user)
    {
        // Act & Assert
        Assert.Throws<EntityNotFoundException>(() =>
            _userRepository.DeleteUser(user));
    }

    [Theory, AutoData]
    public void FindUsersByGameId_WhenUsersExist_ReturnsUsers(Guid gameId, User user1, User user2, User user3)
    {
        // Arrange
        user1.GameId = gameId;
        user2.GameId = gameId;

        var usersData = new List<User> { user1, user2, user3 }.AsQueryable();

        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Provider).Returns(usersData.Provider);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.Expression).Returns(usersData.Expression);
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.ElementType).Returns(usersData.ElementType);
        using var enumerator = usersData.GetEnumerator();
        _mockUsersDbSet.As<IQueryable<User>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        // Act
        var result = _userRepository.FindUsersByGameId(gameId).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, u => Assert.Equal(gameId, u.GameId));
    }
}
