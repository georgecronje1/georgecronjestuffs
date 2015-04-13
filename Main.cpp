#include <stdio.h>


int mapX = 8;
int mapY = 8;
int tileSize = 100;

int main()
{
	int area = mapX*mapY;
	// Create tile type array (vector)
	// Populate and rework
	for (int i = 0; i < area; i++)
	{
		int tempX = (i / mapX) * tileSize;
		int tempY = (i % mapY) * tileSize;
		printf("The X %d and the Y %d \n", tempX, tempY);
		// Extract tile type item from array
		// Create instance at position tempX, tempY
	}
	getchar();
}