using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

public class SpecialtyServiceTests
{
    [Fact]
    public async Task GetAllAsync_DefaultsToActiveOnly()
    {
        var repo = new Mock<ISpecialtyRepository>();
        repo.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Specialty.Create("Physical Therapy", "PHYSICAL_THERAPY", null)
            ]);
        var sut = new SpecialtyService(repo.Object);

        var result = await sut.GetAllAsync();

        var specialty = Assert.Single(result);
        Assert.Equal("Physical Therapy", specialty.Name);
        repo.Verify(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ThrowsValidationException()
    {
        var repo = new Mock<ISpecialtyRepository>();
        repo.Setup(r => r.CodeExistsAsync("pain doctors", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new SpecialtyService(repo.Object);

        await Assert.ThrowsAsync<ValidationException>(() =>
            sut.CreateAsync(new CreateSpecialtyRequest
            {
                Name = "Pain Doctors",
                Code = "pain doctors"
            }));

        repo.Verify(r => r.AddAsync(It.IsAny<Specialty>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NormalizesCodeAndSaves()
    {
        Specialty? added = null;
        var repo = new Mock<ISpecialtyRepository>();
        repo.Setup(r => r.CodeExistsAsync("spine", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<Specialty>(), It.IsAny<CancellationToken>()))
            .Callback<Specialty, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new SpecialtyService(repo.Object);

        var result = await sut.CreateAsync(new CreateSpecialtyRequest
        {
            Name = "Spine",
            Code = "spine"
        });

        Assert.NotNull(added);
        Assert.Equal("SPINE", added!.Code);
        Assert.Equal("SPINE", result.Code);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsAndActiveStatus()
    {
        var id = Guid.CreateVersion7();
        var specialty = Specialty.Create("Old", "OLD", null);

        var repo = new Mock<ISpecialtyRepository>();
        repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(specialty);
        repo.Setup(r => r.CodeExistsAsync("neurology", id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new SpecialtyService(repo.Object);

        var result = await sut.UpdateAsync(id, new UpdateSpecialtyRequest
        {
            Name = "Neurology",
            Code = "neurology",
            IsActive = false
        });

        Assert.Equal("Neurology", result.Name);
        Assert.Equal("NEUROLOGY", result.Code);
        Assert.False(result.IsActive);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_MarksInactive()
    {
        var id = Guid.CreateVersion7();
        var specialty = Specialty.Create("Imaging", "IMAGING", null);

        var repo = new Mock<ISpecialtyRepository>();
        repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(specialty);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new SpecialtyService(repo.Object);

        await sut.DeactivateAsync(id);

        Assert.False(specialty.IsActive);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
