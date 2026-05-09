#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);
extern "C" UIViewController *UnityGetGLViewController(void);

@interface SJJMusicPickerDelegate : NSObject <UIDocumentPickerDelegate>
@property(nonatomic, copy) NSString *gameObjectName;
@end

@implementation SJJMusicPickerDelegate

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller
{
    UnitySendMessage(self.gameObjectName.UTF8String, "OnNativeMusicPickCancelled", "");
}

- (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls
{
    NSURL *sourceURL = urls.firstObject;
    if (sourceURL == nil)
    {
        UnitySendMessage(self.gameObjectName.UTF8String, "OnNativeMusicPickCancelled", "未选择音乐文件。");
        return;
    }

    BOOL scoped = [sourceURL startAccessingSecurityScopedResource];
    NSError *error = nil;
    NSFileManager *fileManager = NSFileManager.defaultManager;
    NSURL *documentsURL = [fileManager URLsForDirectory:NSDocumentDirectory inDomains:NSUserDomainMask].firstObject;
    NSString *extension = sourceURL.pathExtension.length > 0 ? sourceURL.pathExtension : @"mp3";
    NSString *fileName = [NSString stringWithFormat:@"SweetJumpJumpImportedMusic.%@", extension];
    NSURL *destinationURL = [documentsURL URLByAppendingPathComponent:fileName];

    if ([fileManager fileExistsAtPath:destinationURL.path])
    {
        [fileManager removeItemAtURL:destinationURL error:nil];
    }

    BOOL copied = [fileManager copyItemAtURL:sourceURL toURL:destinationURL error:&error];
    if (scoped)
    {
        [sourceURL stopAccessingSecurityScopedResource];
    }

    if (!copied)
    {
        NSString *message = error.localizedDescription ?: @"导入音乐失败。";
        UnitySendMessage(self.gameObjectName.UTF8String, "OnNativeMusicPickCancelled", message.UTF8String);
        return;
    }

    UnitySendMessage(self.gameObjectName.UTF8String, "OnNativeMusicPicked", destinationURL.path.UTF8String);
}

@end

static SJJMusicPickerDelegate *sjjMusicPickerDelegate;

extern "C" void SJJ_OpenMusicPicker(const char *gameObjectName)
{
    NSString *targetGameObject = [NSString stringWithUTF8String:gameObjectName ?: ""];
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *rootController = UnityGetGLViewController();
        if (rootController == nil)
        {
            UnitySendMessage(targetGameObject.UTF8String, "OnNativeMusicPickCancelled", "无法打开文件选择器。");
            return;
        }

        sjjMusicPickerDelegate = [SJJMusicPickerDelegate new];
        sjjMusicPickerDelegate.gameObjectName = targetGameObject;

        UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc] initWithDocumentTypes:@[@"public.audio", @"public.mp3", @"com.apple.music.mp3"] inMode:UIDocumentPickerModeImport];
        picker.delegate = sjjMusicPickerDelegate;
        picker.allowsMultipleSelection = NO;
        picker.modalPresentationStyle = UIModalPresentationFormSheet;
        [rootController presentViewController:picker animated:YES completion:nil];
    });
}
