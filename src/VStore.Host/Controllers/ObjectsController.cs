﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Http.Core.Controllers;
using NuClear.VStore.Http.Core.Extensions;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.S3;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{api-version:apiVersion}/objects")]
    public sealed class ObjectsController : VStoreController
    {
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly ObjectsManagementService _objectsManagementService;
        private readonly ILogger<ObjectsController> _logger;

        public ObjectsController(
            ObjectsStorageReader objectsStorageReader,
            ObjectsManagementService objectsManagementService,
            ILogger<ObjectsController> logger)
        {
            _logger = logger;
            _objectsStorageReader = objectsStorageReader;
            _objectsManagementService = objectsManagementService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyCollection<IdentifyableObjectRecord<long>>), 200)]
        public async Task<IActionResult> List([FromHeader(Name = Http.HeaderNames.AmsContinuationToken)]string continuationToken)
        {
            var container = await _objectsStorageReader.List(continuationToken?.Trim('"'));

            if (!string.IsNullOrEmpty(container.ContinuationToken))
            {
                Response.Headers[Http.HeaderNames.AmsContinuationToken] = $"\"{container.ContinuationToken}\"";
            }

            return Json(container.Collection);
        }

        [HttpGet("specified")]
        [ProducesResponseType(typeof(IReadOnlyCollection<ObjectMetadataRecord>), 200)]
        public async Task<IActionResult> List(IReadOnlyCollection<long> ids)
        {
            var records = await _objectsStorageReader.GetObjectMetadatas(ids);
            return Json(records);
        }

        [HttpGet("{id:long}/{versionId}/template")]
        [ResponseCache(Duration = 120)]
        [ProducesResponseType(typeof(IVersionedTemplateDescriptor), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetTemplateDescriptor(long id, string versionId)
        {
            try
            {
                var templateDescriptor = await _objectsStorageReader.GetTemplateDescriptor(id, versionId);

                Response.Headers[HeaderNames.ETag] = $"\"{templateDescriptor.VersionId}\"";
                Response.Headers[HeaderNames.LastModified] = templateDescriptor.LastModified.ToString("R");
                return Json(templateDescriptor);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id:long}/versions")]
        [ProducesResponseType(typeof(IReadOnlyCollection<ObjectVersionRecord>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(503)]
        public async Task<IActionResult> GetVersions(long id)
        {
            try
            {
                var versions = await _objectsStorageReader.GetObjectVersions(id, null);
                return Json(versions);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (LockAlreadyExistsException)
            {
                return ServiceUnavailable("Simultaneous object versions listing and its creation/modification");
            }
        }

        [HttpGet("{id:long}")]
        [ResponseCache(Duration = 120)]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(304)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(long id, [FromHeader(Name = HeaderNames.IfNoneMatch)] string ifNoneMatch)
        {
            try
            {
                var objectDescriptor = await _objectsStorageReader.GetObjectDescriptor(id, null);

                Response.Headers[HeaderNames.ETag] = $"\"{objectDescriptor.VersionId}\"";
                Response.Headers[HeaderNames.LastModified] = objectDescriptor.LastModified.ToString("R");

                if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch.Trim('"') == objectDescriptor.VersionId)
                {
                    return NotModified();
                }

                return Json(
                    new
                    {
                        objectDescriptor.Id,
                        objectDescriptor.VersionId,
                        objectDescriptor.LastModified,
                        objectDescriptor.TemplateId,
                        objectDescriptor.TemplateVersionId,
                        objectDescriptor.Language,
                        objectDescriptor.Metadata.Author,
                        objectDescriptor.Metadata.AuthorLogin,
                        objectDescriptor.Metadata.AuthorName,
                        objectDescriptor.Properties,
                        objectDescriptor.Elements
                    });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id:long}/{versionId}")]
        [ResponseCache(Duration = 120)]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetVersion(long id, string versionId)
        {
            try
            {
                var objectDescriptor = await _objectsStorageReader.GetObjectDescriptor(id, versionId);

                Response.Headers[HeaderNames.ETag] = $"\"{objectDescriptor.VersionId}\"";
                Response.Headers[HeaderNames.LastModified] = objectDescriptor.LastModified.ToString("R");
                return Json(
                    new
                    {
                        objectDescriptor.Id,
                        objectDescriptor.VersionId,
                        objectDescriptor.LastModified,
                        objectDescriptor.TemplateId,
                        objectDescriptor.TemplateVersionId,
                        objectDescriptor.Language,
                        objectDescriptor.Metadata.Author,
                        objectDescriptor.Metadata.AuthorLogin,
                        objectDescriptor.Metadata.AuthorName,
                        objectDescriptor.Properties,
                        objectDescriptor.Elements
                    });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:long}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> Create(
            long id,
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorLogin)] string authorLogin,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorName)] string authorName,
            [FromBody] IObjectDescriptor objectDescriptor)
        {
            if (string.IsNullOrEmpty(author) || string.IsNullOrEmpty(authorLogin) || string.IsNullOrEmpty(authorName))
            {
                return BadRequest(
                    $"'{Http.HeaderNames.AmsAuthor}', '{Http.HeaderNames.AmsAuthorLogin}' and '{Http.HeaderNames.AmsAuthorName}' " +
                    "request headers must be specified.");
            }

            if (objectDescriptor == null)
            {
                return BadRequest("Object descriptor must be set.");
            }

            try
            {
                var versionId = await _objectsManagementService.Create(id, new AuthorInfo(author, authorLogin, authorName), objectDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Objects", new { id, versionId });

                Response.Headers[HeaderNames.ETag] = $"\"{versionId}\"";
                return Created(url, null);
            }
            catch (InvalidObjectException ex)
            {
                return Unprocessable(ex.SerializeToJson());
            }
            catch (ObjectNotFoundException ex)
            {
                _logger.LogError(new EventId(), ex, "Error occured while creating object");
                return Unprocessable(ex.Message);
            }
            catch (ObjectAlreadyExistsException)
            {
                return Conflict("Object with the same id already exists");
            }
            catch (LockAlreadyExistsException)
            {
                return Conflict("Simultaneous creation of object with the same id");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(new EventId(), ex, "Error occured while creating object");
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id:long}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(412)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> Modify(
            long id,
            [FromHeader(Name = HeaderNames.IfMatch)] string ifMatch,
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorLogin)] string authorLogin,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorName)] string authorName,
            [FromBody] IObjectDescriptor objectDescriptor)
        {
            if (string.IsNullOrEmpty(author) || string.IsNullOrEmpty(authorLogin) || string.IsNullOrEmpty(authorName))
            {
                return BadRequest(
                    $"'{Http.HeaderNames.AmsAuthor}', '{Http.HeaderNames.AmsAuthorLogin}' and '{Http.HeaderNames.AmsAuthorName}' " +
                    "request headers must be specified.");
            }

            if (string.IsNullOrEmpty(author))
            {
                return BadRequest($"'{Http.HeaderNames.AmsAuthor}' request header must be specified.");
            }

            if (objectDescriptor == null)
            {
                return BadRequest("Object descriptor must be set.");
            }

            try
            {
                var latestVersionId = await _objectsManagementService.Modify(
                                          id,
                                          ifMatch.Trim('"'),
                                          new AuthorInfo(author, authorLogin, authorName),
                                          objectDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Objects", new { id, versionId = latestVersionId });

                Response.Headers[HeaderNames.ETag] = $"\"{latestVersionId}\"";
                return NoContent(url);
            }
            catch (InvalidObjectException ex)
            {
                return Unprocessable(ex.SerializeToJson());
            }
            catch (ObjectNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (LockAlreadyExistsException)
            {
                return Conflict("Simultaneous modification of object");
            }
            catch (ConcurrencyException)
            {
                return PreconditionFailed();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(new EventId(), ex, "Error occured while modifying object");
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
