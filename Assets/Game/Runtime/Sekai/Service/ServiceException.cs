using System;

namespace Sekai.Service
{
	public class ServiceException : Exception
	{
				private int _HttpStatusCode;
		public int HttpStatusCode
		{
		    get => _HttpStatusCode;
		    set => _HttpStatusCode = value;
		}

		public ServiceException(string message)
		{
			throw null;
		}

		public ServiceException(int httpStatusCode, string message)
		{
			throw null;
		}
	}
}
