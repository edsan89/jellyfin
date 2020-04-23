#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Security;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Devices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Jellyfin.Api.Controllers
{
    /// <summary>
    /// Devices Controller.
    /// </summary>
    [Authenticated]
    public class DevicesController : BaseJellyfinApiController
    {
        private readonly IDeviceManager _deviceManager;
        private readonly IAuthenticationRepository _authenticationRepository;
        private readonly ISessionManager _sessionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DevicesController"/> class.
        /// </summary>
        /// <param name="deviceManager">Instance of <see cref="IDeviceManager"/> interface.</param>
        /// <param name="authenticationRepository">Instance of <see cref="IAuthenticationRepository"/> interface.</param>
        /// <param name="sessionManager">Instance of <see cref="ISessionManager"/> interface.</param>
        public DevicesController(
            IDeviceManager deviceManager,
            IAuthenticationRepository authenticationRepository,
            ISessionManager sessionManager)
        {
            _deviceManager = deviceManager;
            _authenticationRepository = authenticationRepository;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Get Devices.
        /// </summary>
        /// <param name="supportsSync">/// Gets or sets a value indicating whether [supports synchronize].</param>
        /// <param name="userId">/// Gets or sets the user identifier.</param>
        /// <returns>Device Infos.</returns>
        [HttpGet]
        [Authenticated(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<DeviceInfo[]> GetDevices([FromQuery] bool? supportsSync, [FromQuery] Guid? userId)
        {
            var deviceQuery = new DeviceQuery { SupportsSync = supportsSync, UserId = userId ?? Guid.Empty };
            var devices = _deviceManager.GetDevices(deviceQuery);
            return Ok(devices);
        }

        /// <summary>
        /// Get info for a device.
        /// </summary>
        /// <param name="id">Device Id.</param>
        /// <returns>Device Info.</returns>
        [HttpGet("Info")]
        [Authenticated(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<DeviceInfo> GetDeviceInfo([FromQuery, BindRequired] string id)
        {
            var deviceInfo = _deviceManager.GetDevice(id);
            if (deviceInfo == null)
            {
                return NotFound();
            }

            return deviceInfo;
        }

        /// <summary>
        /// Get options for a device.
        /// </summary>
        /// <param name="id">Device Id.</param>
        /// <returns>Device Info.</returns>
        [HttpGet("Options")]
        [Authenticated(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<DeviceOptions> GetDeviceOptions([FromQuery, BindRequired] string id)
        {
            var deviceInfo = _deviceManager.GetDeviceOptions(id);
            if (deviceInfo == null)
            {
                return NotFound();
            }

            return deviceInfo;
        }

        /// <summary>
        /// Update device options.
        /// </summary>
        /// <param name="id">Device Id.</param>
        /// <param name="deviceOptions">Device Options.</param>
        /// <returns>Status.</returns>
        [HttpPost("Options")]
        [Authenticated(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult UpdateDeviceOptions(
            [FromQuery, BindRequired] string id,
            [FromBody, BindRequired] DeviceOptions deviceOptions)
        {
            var existingDeviceOptions = _deviceManager.GetDeviceOptions(id);
            if (existingDeviceOptions == null)
            {
                return NotFound();
            }

            _deviceManager.UpdateDeviceOptions(id, deviceOptions);
            return Ok();
        }

        /// <summary>
        /// Deletes a device.
        /// </summary>
        /// <param name="id">Device Id.</param>
        /// <returns>Status.</returns>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult DeleteDevice([FromQuery, BindRequired] string id)
        {
            var sessions = _authenticationRepository.Get(new AuthenticationInfoQuery { DeviceId = id }).Items;

            foreach (var session in sessions)
            {
                _sessionManager.Logout(session);
            }

            return Ok();
        }

        /// <summary>
        /// Gets camera upload history for a device.
        /// </summary>
        /// <param name="id">Device Id.</param>
        /// <returns>Content Upload History.</returns>
        [HttpGet("CameraUploads")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<ContentUploadHistory> GetCameraUploads([FromQuery, BindRequired] string id)
        {
            var uploadHistory = _deviceManager.GetCameraUploadHistory(id);
            return uploadHistory;
        }

        /// <summary>
        /// Uploads content.
        /// </summary>
        /// <param name="deviceId">Device Id.</param>
        /// <param name="album">Album.</param>
        /// <param name="name">Name.</param>
        /// <param name="id">Id.</param>
        /// <returns>Status.</returns>
        [HttpPost("CameraUploads")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> PostCameraUploadAsync(
            [FromQuery, BindRequired] string deviceId,
            [FromQuery, BindRequired] string album,
            [FromQuery, BindRequired] string name,
            [FromQuery, BindRequired] string id)
        {
            Stream fileStream;
            string contentType;

            if (Request.HasFormContentType)
            {
                if (Request.Form.Files.Any())
                {
                    fileStream = Request.Form.Files[0].OpenReadStream();
                    contentType = Request.Form.Files[0].ContentType;
                }
                else
                {
                    return BadRequest();
                }
            }
            else
            {
                fileStream = Request.Body;
                contentType = Request.ContentType;
            }

            await _deviceManager.AcceptCameraUpload(
                deviceId,
                fileStream,
                new LocalFileInfo { MimeType = contentType, Album = album, Name = name, Id = id }).ConfigureAwait(false);

            return Ok();
        }
    }
}
