#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestServiceHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class PublishManifestCommandTests
    {
        /// <summary>
        /// Verifies manifest lists are created and pushed for images with shared tags.
        /// Digest saving has moved to CreateManifestListCommand, so this command
        /// should NOT modify image-info.json.
        /// </summary>
        [Fact]
        public async Task CreatesAndPushesManifestLists()
        {
            Mock<IDockerService> dockerServiceMock = new();

            PublishManifestCommand command = CreateCommand(dockerServiceMock);

            using TempFolderContext tempFolderContext = new TempFolderContext();

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfile1 = CreateDockerfile("1.0/repo1/os", tempFolderContext);
            string dockerfile2 = CreateDockerfile("1.0/repo2/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile1, simpleTags: new List<string>{ "tag1" }),
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag1",
                                        "sharedtag2"
                                    }
                                }
                            }
                        }
                    },
                }
            };
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1, new string[] { "tag1" })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "sharedtag2", new Tag() },
                            { "sharedtag1", new Tag() }
                        })));
            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            dockerServiceMock.Verify(o => o.CreateManifestList("repo1:sharedtag1", new string[] { "repo1:tag1" }, false));
            dockerServiceMock.Verify(o => o.CreateManifestList("repo1:sharedtag2", new string[] { "repo1:tag1" }, false));

            dockerServiceMock.Verify(o => o.PushManifestList("repo1:sharedtag1", false));
            dockerServiceMock.Verify(o => o.PushManifestList("repo1:sharedtag2", false));
        }

        /// <summary>
        /// Verifies a correct manifest is generated when there are duplicate platforms defined, with some not containing
        /// concrete tags.
        /// </summary>
        [Fact]
        public async Task DuplicatePlatform()
        {
            Mock<IDockerService> dockerServiceMock = new();

            PublishManifestCommand command = CreateCommand(dockerServiceMock);

            using TempFolderContext tempFolderContext = new TempFolderContext();

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfile = CreateDockerfile("1.0/repo1/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile,
                                        simpleTags: new List<string>
                                        {
                                            "tag1",
                                            "tag2"
                                        })
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag1",
                                        "sharedtag2"
                                    }
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile)
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag3"
                                    }
                                }
                            }
                        }
                    }
                }
            };
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile,
                                new string[]
                                {
                                    "tag1",
                                    "tag2"
                                })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "sharedtag2", new Tag() },
                            { "sharedtag1", new Tag() }
                        }),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile, Array.Empty<string>())
                        },
                        new Dictionary<string, Tag>
                        {
                            { "sharedtag3", new Tag() }
                        }))
            );
            manifest.Registry = "mcr.microsoft.com";
            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            dockerServiceMock.Verify(o => o.CreateManifestList("mcr.microsoft.com/repo1:sharedtag1", new string[] { "mcr.microsoft.com/repo1:tag1" }, false));
            dockerServiceMock.Verify(o => o.CreateManifestList("mcr.microsoft.com/repo1:sharedtag2", new string[] { "mcr.microsoft.com/repo1:tag1" }, false));
            dockerServiceMock.Verify(o => o.CreateManifestList("mcr.microsoft.com/repo1:sharedtag3", new string[] { "mcr.microsoft.com/repo1:tag1" }, false));

            dockerServiceMock.Verify(o => o.PushManifestList("mcr.microsoft.com/repo1:sharedtag1", false));
            dockerServiceMock.Verify(o => o.PushManifestList("mcr.microsoft.com/repo1:sharedtag2", false));
            dockerServiceMock.Verify(o => o.PushManifestList("mcr.microsoft.com/repo1:sharedtag3", false));

            dockerServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Verifies a correct manifest is generated when syndicating tags to another repo.
        /// </summary>
        [Fact]
        public async Task SyndicatedTag()
        {
            Mock<IDockerService> dockerServiceMock = new();

            PublishManifestCommand command = CreateCommand(dockerServiceMock);

            using TempFolderContext tempFolderContext = new TempFolderContext();

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfile1 = CreateDockerfile("1.0/repo/os", tempFolderContext);
            string dockerfile2 = CreateDockerfile("1.0/repo/os2", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile1,
                                        simpleTags: new List<string>
                                        {
                                            "tag1",
                                            "tag2"
                                        }),
                                    CreatePlatform(dockerfile2,
                                        simpleTags: new List<string>
                                        {
                                            "tag3"
                                        })
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag1",
                                        "sharedtag2"
                                    }
                                }
                            }
                        }
                    }
                }
            };
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            const string syndicatedRepo2 = "repo2";

            Platform platform1;
            Platform platform2;

            Manifest manifest = CreateManifest(
                CreateRepo("repo",
                    CreateImage(
                        new Platform[]
                        {
                            platform1 = CreatePlatform(dockerfile1, Array.Empty<string>()),
                            platform2 = CreatePlatform(dockerfile2, Array.Empty<string>())
                        },
                        new Dictionary<string, Tag>
                        {
                            {
                                "sharedtag2",
                                new Tag
                                {
                                    Syndication = new TagSyndication
                                    {
                                        Repo = syndicatedRepo2,
                                        DestinationTags = new string[]
                                        {
                                            "sharedtag2a",
                                            "sharedtag2b"
                                        }
                                    }
                                }
                            },
                            { "sharedtag1", new Tag() }
                        }))
            );

            manifest.Registry = "mcr.microsoft.com";
            platform1.Tags = new Dictionary<string, Tag>
            {
                { "tag1", new Tag() },
                { "tag2", new Tag
                    {
                        Syndication = new TagSyndication
                        {
                            Repo = syndicatedRepo2,
                            DestinationTags = new string[]
                            {
                                "tag2"
                            }
                        }
                    }
                },
            };

            platform2.Tags = new Dictionary<string, Tag>
            {
                { "tag3", new Tag() }
            };

            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            dockerServiceMock.Verify(o => o.CreateManifestList("mcr.microsoft.com/repo:sharedtag1", new string[] { "mcr.microsoft.com/repo:tag1", "mcr.microsoft.com/repo:tag3" }, false));
            dockerServiceMock.Verify(o => o.CreateManifestList("mcr.microsoft.com/repo:sharedtag2", new string[] { "mcr.microsoft.com/repo:tag1", "mcr.microsoft.com/repo:tag3" }, false));
            dockerServiceMock.Verify(o => o.CreateManifestList("mcr.microsoft.com/repo2:sharedtag2a", new string[] { "mcr.microsoft.com/repo2:tag2" }, false));
            dockerServiceMock.Verify(o => o.CreateManifestList("mcr.microsoft.com/repo2:sharedtag2b", new string[] { "mcr.microsoft.com/repo2:tag2" }, false));

            dockerServiceMock.Verify(o => o.PushManifestList("mcr.microsoft.com/repo:sharedtag1", false));
            dockerServiceMock.Verify(o => o.PushManifestList("mcr.microsoft.com/repo:sharedtag2", false));
            dockerServiceMock.Verify(o => o.PushManifestList("mcr.microsoft.com/repo2:sharedtag2a", false));
            dockerServiceMock.Verify(o => o.PushManifestList("mcr.microsoft.com/repo2:sharedtag2b", false));
        }

        /// <summary>
        /// Verifies that manifests lists that don't contain any changed images aren't created/pushed.
        /// </summary>
        [Fact]
        public async Task UnchangedManifestList()
        {
            Mock<IDockerService> dockerServiceMock = new();

            PublishManifestCommand command = CreateCommand(dockerServiceMock);

            using TempFolderContext tempFolderContext = new();
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfile1 = CreateDockerfile("1.0/repo1/changedPlatform", tempFolderContext);
            string dockerfile2 = CreateDockerfile("1.0/repo1/unchangedPlatform", tempFolderContext);

            Manifest manifest =
                CreateManifest(
                    CreateRepo("repo1",
                        CreateImage(
                            sharedTags: ["changedPlatform-sharedtag"],
                            CreatePlatform(dockerfile1, ["changedPlatform"])),
                        CreateImage(
                            sharedTags: ["unchangedPlatform-sharedtag"],
                            CreatePlatform(dockerfile2, ["unchangedPlatform"]))));

            ImageArtifactDetails imageArtifactDetails =
                CreateImageArtifactDetails(
                    CreateRepoData("repo1",
                        CreateImageData(
                            sharedTags: ["changedPlatform-sharedtag"],
                            CreatePlatform(
                                dockerfile: dockerfile1,
                                simpleTags: ["changedPlatform"])),
                        CreateImageData(
                            sharedTags: ["unchangedPlatform-sharedtag"],
                            CreatePlatform(
                                dockerfile: dockerfile1,
                                simpleTags: ["unchangedPlatform"],
                                isUnchanged: true))));

            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            await command.ExecuteAsync();

            // Verify that the changed platform's shared tag was created
            dockerServiceMock.Verify(o =>
                o.CreateManifestList(
                    "repo1:changedPlatform-sharedtag",
                    new string[] { "repo1:changedPlatform" },
                    false),
                Times.Once);

            // Verify that the unchanged platform's shared tag was not created
            dockerServiceMock.Verify(o =>
                o.CreateManifestList(
                    "repo1:unchangedPlatform-sharedtag",
                    new string[] { "repo1:unchangedPlatform" },
                    false),
                Times.Never);
        }

        private static PublishManifestCommand CreateCommand(Mock<IDockerService> dockerServiceMock) =>
            new(
                TestHelper.CreateManifestJsonService(),
                dockerServiceMock.Object,
                Mock.Of<ILogger<PublishManifestCommand>>(),
                Mock.Of<IRegistryCredentialsProvider>(),
                new ManifestListService());
    }
}
