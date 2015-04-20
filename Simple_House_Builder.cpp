int length = 10;
int woodLenLong = 2;
int woodLenShort = 1;
int width = 5;
int height = 3;
bool door = true;

void Main()
{
	for (int y=0;y<legnth;y++) 
	{
		for (int x=0;x<width;x++) 
		{
			for (int z=0;z<height;z++) 
			{
				if ((x == 0) && (y == 0)) 
				{
					//Bottom left corner
				}
				if ((x == 0) && (y > 0) && (y < legnth)) 
				{
					// Left wall
				}
				if ((x == 0) && (y == length))
				{
					// Top Left corner
				}
				if((y == 0) && (x > 0) && (x < width))
				{
					// Bottom wall
				}
				if ((y == 0) && (x == width))
				{
					// Bottom right corner
				}
				if ((x == width) && (y  > 0) && (y < length))
				{
					// left Wall
				}
				if ((x == width) && (y == length))
				{
					// top left corner
				}
				
			}
		}
	}
}

// Define other methods and classes here
