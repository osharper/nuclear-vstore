To build image locally from PowerShell:

```
docker-compose -f docker-compose-build.yml up
docker build publish\vstore -t vstore:latest --build-arg AWS_ACCESS_KEY_ID=${env:AWS_ACCESS_KEY_ID} --build-arg AWS_SECRET_ACCESS_KEY=${env:AWS_SECRET_ACCESS_KEY}
```

Next, you can move the image to Ubuntu machine with Docker installed:

```
docker save vstore -o vstore.image
scp vstore.image ubuntu@vstore.erm.ostack.test:~
```

Next, on the Ubuntu machine:

```
sudo docker load -i vstore.image
sudo docker run -d -p 5000:5000 vstore
```